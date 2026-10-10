using System.Text.Json;
using System.Text.Json.Serialization;
using NflCoverage.Simulation;

namespace NflCoverage.Web.Client.Models;

public sealed record TeamInfo(string Id, string Name, string Conference, string Division, string PrimaryColor, string? LogoUrl);

/// <summary>Payload for the what-if simulator: the season state plus display info for each team.</summary>
public sealed record WhatIfData(SimulationInput Input, IReadOnlyList<TeamInfo> Teams);

/// <summary>Source-generated serialization keeps the trimmed WebAssembly bundle reflection-free.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(WhatIfData))]
public sealed partial class ClientJsonContext : JsonSerializerContext;
