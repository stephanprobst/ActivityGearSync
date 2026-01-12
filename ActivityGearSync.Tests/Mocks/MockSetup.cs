using ActivityGearSync.Interfaces;
using Imposter.Abstractions;

[assembly: GenerateImposter(typeof(IStravaApiClient))]
[assembly: GenerateImposter(typeof(IStravaAuthClient))]
[assembly: GenerateImposter(typeof(IGitHubReleaseClient))]
