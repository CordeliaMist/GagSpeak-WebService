﻿using System.Collections.Concurrent;
using GagspeakServer.Metrics;
using GagspeakServer.Data;
using GagspeakServer.Services;
using GagspeakServer.Utils.Configuration;
using Microsoft.EntityFrameworkCore;

namespace GagspeakServer.Authentication;

public class SecretKeyAuthenticatorService
{
    private readonly GagspeakMetrics _metrics;
    private readonly IDbContextFactory<GagspeakDbContext> _dbContextFactory;
    private readonly IConfigurationService<AuthServiceConfiguration> _configurationService;
    private readonly ILogger<SecretKeyAuthenticatorService> _logger;
    private readonly ConcurrentDictionary<string, SecretKeyFailedAuthorization> _failedAuthorizations = new(StringComparer.Ordinal);

    public SecretKeyAuthenticatorService(GagspeakMetrics metrics, IDbContextFactory<GagspeakDbContext> dbContextFactory,
        IConfigurationService<AuthServiceConfiguration> configuration, ILogger<SecretKeyAuthenticatorService> logger)
    {
        _logger = logger;
        _configurationService = configuration;
        _metrics = metrics;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<SecretKeyAuthReply> AuthorizeAsync(string ip, string hashedSecretKey)
    {
        _metrics.IncCounter(MetricsAPI.CounterAuthenticationRequests);

        if (_failedAuthorizations.TryGetValue(ip, out var existingFailedAuthorization)
            && existingFailedAuthorization.FailedAttempts > _configurationService.GetValueOrDefault(nameof(AuthServiceConfiguration.FailedAuthForTempBan), 5))
        {
            if (existingFailedAuthorization.ResetTask == null)
            {
                _logger.LogWarning("TempBan {ip} for authorization spam", ip);

                existingFailedAuthorization.ResetTask = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(_configurationService.GetValueOrDefault(nameof(AuthServiceConfiguration.TempBanDurationInMinutes), 5))).ConfigureAwait(false);

                }).ContinueWith((t) =>
                {
                    _failedAuthorizations.Remove(ip, out _);
                });
            }
            return new(Success: false, Uid: null, PrimaryUid: null, Alias: null, TempBan: true, Permaban: false);
        }

        using var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
        var authReply = await context.Auth.Include(a => a.User).AsNoTracking()
            .SingleOrDefaultAsync(u => u.HashedKey == hashedSecretKey).ConfigureAwait(false);
        var isBanned = authReply?.IsBanned ?? false;
        var primaryUid = authReply?.PrimaryUserUID ?? authReply?.UserUID;

        if (authReply?.PrimaryUserUID != null)
        {
            var primaryUser = await context.Auth.AsNoTracking().SingleOrDefaultAsync(u => u.UserUID == authReply.PrimaryUserUID).ConfigureAwait(false);
            isBanned = isBanned || primaryUser.IsBanned;
        }

        SecretKeyAuthReply reply = new(authReply != null, authReply?.UserUID,
            authReply?.PrimaryUserUID ?? authReply?.UserUID, authReply?.User?.Alias ?? string.Empty, TempBan: false, isBanned);

        if (reply.Success)
        {
            _metrics.IncCounter(MetricsAPI.CounterAuthenticationSuccesses);
            _metrics.IncGauge(MetricsAPI.GaugeAuthenticationCacheEntries);
        }
        else
        {
            return AuthenticationFailure(ip);
        }

        return reply;
    }

    private SecretKeyAuthReply AuthenticationFailure(string ip)
    {
        _metrics.IncCounter(MetricsAPI.CounterAuthenticationFailures);

        _logger.LogWarning("Failed authorization from {ip}", ip);
        var whitelisted = _configurationService.GetValueOrDefault(nameof(AuthServiceConfiguration.WhitelistedIps), new List<string>());
        if (!whitelisted.Exists(w => ip.Contains(w, StringComparison.OrdinalIgnoreCase)))
        {
            if (_failedAuthorizations.TryGetValue(ip, out var auth))
            {
                auth.IncreaseFailedAttempts();
            }
            else
            {
                _failedAuthorizations[ip] = new SecretKeyFailedAuthorization();
            }
        }

        return new(Success: false, Uid: null, PrimaryUid: null, Alias: null, TempBan: false, Permaban: false);
    }
}
