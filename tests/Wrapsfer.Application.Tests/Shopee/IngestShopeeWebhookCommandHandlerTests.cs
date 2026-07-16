using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Commands.IngestShopeeWebhook;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class IngestShopeeWebhookCommandHandlerTests
{
    private const string ValidBody = """{"code":3,"shop_id":123456,"data":{"ordersn":"SN1","status":"READY_TO_SHIP"}}""";
    private const string UnhandledCodeBody = """{"code":99,"shop_id":123456,"data":{}}""";
    private const string AuthorizationHeader = "signature";

    private readonly Mock<IShopeeWebhookSignatureVerifier> _signatureVerifier = new();
    private readonly Mock<IShopeeWebhookEventRepository> _eventRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly IngestShopeeWebhookCommandHandler _handler;

    public IngestShopeeWebhookCommandHandlerTests() =>
        _handler = new IngestShopeeWebhookCommandHandler(
            _signatureVerifier.Object,
            _eventRepository.Object,
            _unitOfWork.Object);

    [Fact]
    public async Task Handle_ValidSignatureAndNewMessage_AddsPendingEvent()
    {
        _signatureVerifier
            .Setup(v => v.Verify(AuthorizationHeader, ValidBody))
            .Returns(true);
        _eventRepository
            .Setup(r => r.ExistsByMessageKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(
            new IngestShopeeWebhookCommand(ValidBody, AuthorizationHeader), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ShopeeWebhookEvent addedEvent = (ShopeeWebhookEvent)_eventRepository.Invocations
            .Single(i => i.Method.Name == nameof(IShopeeWebhookEventRepository.Add)).Arguments[0];
        addedEvent.Status.Should().Be(ShopeeWebhookEventStatus.Pending);
        addedEvent.ShopId.Should().Be(123456);
        addedEvent.Code.Should().Be(3);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidSignatureAndDuplicateMessage_SucceedsWithoutAdding()
    {
        _signatureVerifier
            .Setup(v => v.Verify(AuthorizationHeader, ValidBody))
            .Returns(true);
        _eventRepository
            .Setup(r => r.ExistsByMessageKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result result = await _handler.Handle(
            new IngestShopeeWebhookCommand(ValidBody, AuthorizationHeader), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _eventRepository.Verify(r => r.Add(It.IsAny<ShopeeWebhookEvent>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidSignature_FailsWithoutStoringAnything()
    {
        _signatureVerifier
            .Setup(v => v.Verify(AuthorizationHeader, ValidBody))
            .Returns(false);

        Result result = await _handler.Handle(
            new IngestShopeeWebhookCommand(ValidBody, AuthorizationHeader), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeWebhookEventErrors.InvalidSignature);
        _eventRepository.Verify(r => r.Add(It.IsAny<ShopeeWebhookEvent>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonJsonBody_FailsWithMalformedPushBody()
    {
        const string body = "not-json";
        _signatureVerifier
            .Setup(v => v.Verify(AuthorizationHeader, body))
            .Returns(true);

        Result result = await _handler.Handle(
            new IngestShopeeWebhookCommand(body, AuthorizationHeader), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeWebhookEventErrors.MalformedPushBody);
        _eventRepository.Verify(r => r.Add(It.IsAny<ShopeeWebhookEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnhandledCode_StoresEventAsIgnored()
    {
        _signatureVerifier
            .Setup(v => v.Verify(AuthorizationHeader, UnhandledCodeBody))
            .Returns(true);
        _eventRepository
            .Setup(r => r.ExistsByMessageKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(
            new IngestShopeeWebhookCommand(UnhandledCodeBody, AuthorizationHeader), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ShopeeWebhookEvent addedEvent = (ShopeeWebhookEvent)_eventRepository.Invocations
            .Single(i => i.Method.Name == nameof(IShopeeWebhookEventRepository.Add)).Arguments[0];
        addedEvent.Status.Should().Be(ShopeeWebhookEventStatus.Ignored);
    }
}
