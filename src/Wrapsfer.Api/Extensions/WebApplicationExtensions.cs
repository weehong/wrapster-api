using System.Net.Sockets;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Wrapsfer.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                IExceptionHandlerFeature? exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                Exception? exception = exceptionFeature?.Error;
                ILogger logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Wrapsfer.Api.ExceptionHandler");

                ProblemDetails problemDetails = ClassifyException(exception, logger);

                problemDetails.Extensions["traceId"] = context.TraceIdentifier;
                if (problemDetails.Status == StatusCodes.Status500InternalServerError)
                {
                    problemDetails.Detail =
                        $"Something went wrong on our side. Quote reference {context.TraceIdentifier} when reporting this.";
                }

                context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(problemDetails);
            });
        });

        app.UseHttpsRedirection();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");

        return app;
    }

    private static bool IsDatabaseException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is PostgresException or NpgsqlException)
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }

    private static T? FindException<T>(Exception? exception) where T : Exception
    {
        while (exception is not null)
        {
            if (exception is T typed)
            {
                return typed;
            }

            exception = exception.InnerException;
        }

        return null;
    }

    private static ProblemDetails ClassifyException(Exception? exception, ILogger logger)
    {
        if (exception is null)
        {
            return CreateProblem(500, "Internal Server Error",
                "An unknown error occurred.", logger, null);
        }

        if (IsDatabaseException(exception))
        {
            return CreateDatabaseProblem(exception, logger);
        }

        return exception switch
        {
            DbUpdateConcurrencyException =>
                CreateProblem(409, "Conflict",
                    "This record was modified by another request while you were working on it. Refresh and try again.",
                    logger, exception),

            DbUpdateException =>
                CreateProblem(409, "Conflict",
                    "The change conflicts with existing data (for example, a duplicate value or missing reference). Refresh and try again.",
                    logger, exception),

            InvalidOperationException { Message: "Tenant realm has not been resolved." } =>
                CreateProblem(401, "Authentication Error",
                    "Tenant could not be identified. Ensure the request includes a valid authorization token.",
                    logger, exception),

            InvalidOperationException { Message: "User is not authenticated." } =>
                CreateProblem(401, "Authentication Error",
                    "User identity could not be determined. Ensure the request includes a valid authorization token.",
                    logger, exception),

            InvalidOperationException when exception.Message.Contains("not configured") =>
                CreateProblem(503, "Configuration Error",
                    "The server is misconfigured. Please contact the administrator.",
                    logger, exception),

            ArgumentException =>
                CreateProblem(400, "Invalid Argument",
                    "The request contains an invalid argument.",
                    logger, exception),

            TimeoutException =>
                CreateProblem(504, "Request Timeout",
                    "The operation timed out. Please try again.",
                    logger, exception),

            _ => CreateProblem(500, "Internal Server Error",
                "An unexpected error occurred. Check the server logs for details.",
                logger, exception)
        };
    }

    private static ProblemDetails CreateProblem(int status, string title, string detail,
        ILogger logger, Exception? exception)
    {
        logger.LogError(exception, "{Title}: {Detail}", title, detail);

        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }

    private static ProblemDetails CreateDatabaseProblem(Exception exception, ILogger logger)
    {
        PostgresException? postgresException = FindException<PostgresException>(exception);
        NpgsqlException? npgsqlException = FindException<NpgsqlException>(exception);

        // Unique-constraint violations are client-resolvable conflicts (e.g. two concurrent
        // creates racing past the duplicate pre-check), not server faults.
        if (postgresException?.SqlState == "23505")
        {
            return CreateProblem(409, "Conflict",
                "The change conflicts with existing data (for example, a duplicate value). Refresh and try again.",
                logger, exception);
        }

        string detail = postgresException?.SqlState switch
        {
            "42P01" =>
                "A required database table does not exist. Run pending migrations.",
            "42703" =>
                "A required database column does not exist. Run pending migrations.",
            "28P01" => "Database authentication failed. Check connection credentials.",
            _ when FindException<SocketException>(exception) is not null =>
                "Cannot reach the database server. Ensure PostgreSQL is running.",
            _ => "A database error occurred. Please check the server logs for details."
        };

        return CreateProblem(503, "Database Error", detail, logger, exception);
    }
}
