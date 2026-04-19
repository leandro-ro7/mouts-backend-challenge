using Ambev.DeveloperEvaluation.Domain.Exceptions;

namespace Ambev.DeveloperEvaluation.Domain.ValueObjects;

/// <summary>
/// Value Object representing a quantity-based discount rate.
/// Immutable, equality by value, encapsulates all discount tier logic.
/// </summary>
public sealed class DiscountRate : IEquatable<DiscountRate>
{
    public decimal Value { get; private set; }

    // Required by EF Core owned entity
    private DiscountRate() { }

    private DiscountRate(decimal value) => Value = value;

    public static readonly DiscountRate None = new(0m);
    public static readonly DiscountRate TenPercent = new(0.10m);
    public static readonly DiscountRate TwentyPercent = new(0.20m);

    // Used by EF Core value converter to reconstruct from a persisted decimal.
    public static DiscountRate FromValue(decimal value) => new(value);

    /// <summary>
    /// Returns the applicable discount rate for a given quantity.
    /// Rule: "purchases above 4 identical items" → qty > 4 is the threshold for 10%.
    /// - qty 1-4:   0%  (at or below threshold — no discount)
    /// - qty 5-9:  10%  (strictly above 4)
    /// - qty 10-20: 20% (both bounds inclusive)
    /// - qty > 20: DomainException (business restriction)
    /// </summary>
    public static DiscountRate For(int quantity)
    {
        if (quantity > 20)
            throw new DomainException($"Cannot sell more than 20 identical items. Requested: {quantity}.");
        if (quantity >= 10) return TwentyPercent;
        if (quantity > 4)  return TenPercent;
        return None;
    }

    /// <summary>Applies the discount to an amount and rounds to 2 decimal places.</summary>
    public decimal Apply(decimal amount) => Math.Round(amount * (1 - Value), 2);

    public bool Equals(DiscountRate? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is DiscountRate d && Equals(d);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"{Value:P0}";

    public static implicit operator decimal(DiscountRate rate) => rate.Value;
}
