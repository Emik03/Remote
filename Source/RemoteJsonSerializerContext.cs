// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <summary>Contains the context used for JSON serialization.</summary>
[JsonSerializable(typeof(JsonNode)), JsonSerializable(typeof(OrderedDictionary<string, HistoryServer>)),
 JsonSourceGenerationOptions(WriteIndented = true)]
sealed partial class RemoteJsonSerializerContext : JsonSerializerContext;
