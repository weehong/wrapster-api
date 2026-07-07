using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using Wrapsfer.Api.Filters;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Infrastructure.Authentication;

namespace Wrapsfer.IntegrationTests.Filters;

public class TenantResolutionFilterTests
{
    private const string OwnerRealm = "wrapsfer";
    private const string TenantRealmKey = "TenantRealm";
    private readonly TenantResolutionFilter _filter;
    private readonly Mock<IPartnerTenantRepository> _partnerTenantRepository = new();

    public TenantResolutionFilterTests() =>
        _filter = new TenantResolutionFilter(
            Options.Create(new KeycloakOptions { OwnerRealm = OwnerRealm }),
            _partnerTenantRepository.Object);

    private static PartnerTenant CreatePartnerTenant(string tenantId, bool isActive)
    {
        PartnerTenant partnerTenant = PartnerTenant.Create(tenantId, "Test Partner", OwnerRealm).Value;
        if (isActive)
        {
            partnerTenant.MarkActive();
        }

        return partnerTenant;
    }

    private static (ActionExecutingContext Context, Func<bool> NextCalled) CreateContext(
        string currentRealm,
        string method,
        string? tenantIdQuery,
        params object[] endpointMetadata)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Items[TenantRealmKey] = currentRealm;
        httpContext.Request.Method = method;
        if (tenantIdQuery is not null)
        {
            httpContext.Request.QueryString = new QueryString($"?tenantId={Uri.EscapeDataString(tenantIdQuery)}");
        }

        ActionContext actionContext = new(httpContext, new RouteData(),
            new ActionDescriptor { EndpointMetadata = endpointMetadata.ToList() });
        ActionExecutingContext context = new(actionContext, new List<IFilterMetadata>(),
            new Dictionary<string, object?>(), new object());
        return (context, () => (bool)httpContext.Items["NextCalled"]!);
    }

    private static ActionExecutionDelegate CreateNext(ActionExecutingContext context)
    {
        context.HttpContext.Items["NextCalled"] = false;
        return () =>
        {
            context.HttpContext.Items["NextCalled"] = true;
            return Task.FromResult(new ActionExecutedContext(context,
                new List<IFilterMetadata>(), new object()));
        };
    }

    [Fact]
    public async Task NonOwnerRealm_PassesThroughWithoutRepositoryLookup()
    {
        (ActionExecutingContext context, Func<bool> nextCalled) =
            CreateContext("some-partner", HttpMethods.Post, tenantIdQuery: null);

        await _filter.OnActionExecutionAsync(context, CreateNext(context));

        nextCalled().Should().BeTrue();
        context.Result.Should().BeNull();
        _partnerTenantRepository.Verify(r =>
            r.GetByTenantIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Owner_WithoutTenantId_ReturnsBadRequest()
    {
        (ActionExecutingContext context, Func<bool> nextCalled) =
            CreateContext(OwnerRealm, HttpMethods.Get, tenantIdQuery: null);

        await _filter.OnActionExecutionAsync(context, CreateNext(context));

        nextCalled().Should().BeFalse();
        context.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Owner_WithoutTenantId_OnCrossTenantEndpoint_SetsScopeFlag()
    {
        (ActionExecutingContext context, Func<bool> nextCalled) = CreateContext(
            OwnerRealm, HttpMethods.Get, tenantIdQuery: null, new AllowOwnerTenantScopeAttribute());

        await _filter.OnActionExecutionAsync(context, CreateNext(context));

        nextCalled().Should().BeTrue();
        context.HttpContext.Items[TenantResolutionFilter.OwnerCrossTenantScopeKey].Should().Be(true);
    }

    [Fact]
    public async Task Owner_WithOwnerRealmAsTenantId_ReturnsBadRequest()
    {
        (ActionExecutingContext context, Func<bool> nextCalled) =
            CreateContext(OwnerRealm, HttpMethods.Get, OwnerRealm);

        await _filter.OnActionExecutionAsync(context, CreateNext(context));

        nextCalled().Should().BeFalse();
        context.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Owner_WithUnknownTenant_ReturnsBadRequest()
    {
        _partnerTenantRepository.Setup(r =>
                r.GetByTenantIdAsync("no-such-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartnerTenant?)null);
        (ActionExecutingContext context, Func<bool> nextCalled) =
            CreateContext(OwnerRealm, HttpMethods.Get, "no-such-tenant");

        await _filter.OnActionExecutionAsync(context, CreateNext(context));

        nextCalled().Should().BeFalse();
        BadRequestObjectResult result = context.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        result.Value.Should().BeOfType<ProblemDetails>()
            .Which.Title.Should().Be("Unknown Tenant");
    }

    [Fact]
    public async Task Owner_WritingToInactiveTenant_ReturnsConflict()
    {
        _partnerTenantRepository.Setup(r =>
                r.GetByTenantIdAsync("ophr168", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePartnerTenant("ophr168", isActive: false));
        (ActionExecutingContext context, Func<bool> nextCalled) =
            CreateContext(OwnerRealm, HttpMethods.Post, "ophr168");

        await _filter.OnActionExecutionAsync(context, CreateNext(context));

        nextCalled().Should().BeFalse();
        ConflictObjectResult result = context.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        result.Value.Should().BeOfType<ProblemDetails>()
            .Which.Title.Should().Be("Tenant Inactive");
    }

    [Fact]
    public async Task Owner_ReadingInactiveTenant_PassesThrough()
    {
        _partnerTenantRepository.Setup(r =>
                r.GetByTenantIdAsync("ophr168", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePartnerTenant("ophr168", isActive: false));
        (ActionExecutingContext context, Func<bool> nextCalled) =
            CreateContext(OwnerRealm, HttpMethods.Get, "ophr168");

        await _filter.OnActionExecutionAsync(context, CreateNext(context));

        nextCalled().Should().BeTrue();
        context.HttpContext.Items[TenantRealmKey].Should().Be("ophr168");
    }

    [Fact]
    public async Task Owner_WithMixedCasePaddedTenantId_NormalizesBeforeLookupAndStore()
    {
        _partnerTenantRepository.Setup(r =>
                r.GetByTenantIdAsync("purepresence168", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePartnerTenant("purepresence168", isActive: true));
        (ActionExecutingContext context, Func<bool> nextCalled) =
            CreateContext(OwnerRealm, HttpMethods.Post, " PurePresence168 ");

        await _filter.OnActionExecutionAsync(context, CreateNext(context));

        nextCalled().Should().BeTrue();
        context.HttpContext.Items[TenantRealmKey].Should().Be("purepresence168");
        _partnerTenantRepository.Verify(r =>
            r.GetByTenantIdAsync("purepresence168", It.IsAny<CancellationToken>()), Times.Once);
    }
}
