using JasperFx;
using Npgsql;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.API;

internal static class ApiFailure
{
    public static AppException? From(Exception? exception) => exception switch
    {
        AppException failure => failure,
        ConcurrencyException
            or DocumentAlreadyExistsException
            or PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure }
            => new AppException("Conflict", 409),
        BadHttpRequestException => new AppException("InvalidRequest"),
        FormatException => new AppException("InvalidInterval"),
        _ => null
    };
}
