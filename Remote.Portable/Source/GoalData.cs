// SPDX-License-Identifier: MPL-2.0
namespace Remote.Portable;

/// <summary>Contains the goal index.</summary>
/// <param name="Goal">The index.</param>
[Serializable] // ReSharper disable once ClassNeverInstantiated.Local
public sealed record GoalData(int Goal) : IEqualityOperators<GoalData, GoalData, bool>;
