using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FSIdentity.Domain.Data;

public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

public class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    value => value.HasValue && value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime() : value,
    value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
