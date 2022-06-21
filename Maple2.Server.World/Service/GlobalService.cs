﻿using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Maple2.Database.Storage;
using Maple2.Model.Game;
using Serilog;

// TODO: Move this to a Global server
// ReSharper disable once CheckNamespace
namespace Maple2.Server.Global.Service;

public partial class GlobalService : Global.GlobalBase {
    private readonly GameStorage gameStorage;

    private readonly ILogger logger = Log.Logger.ForContext<GlobalService>();

    public GlobalService(GameStorage gameStorage) {
        this.gameStorage = gameStorage;
    }

    public override Task<HealthResponse> Health(Empty request, ServerCallContext context) {
        return Task.FromResult(new HealthResponse());
    }

    public override Task<LoginResponse> Login(LoginRequest request, ServerCallContext context) {
#if !DEBUG // Allow empty username for testing
        if (string.IsNullOrWhiteSpace(request.Username)) {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Username must be specified."));
        }
#endif

        // Normalize username
        string username = request.Username.Trim().ToLower();
        var machineId = new Guid(request.MachineId);

        using GameStorage.Request db = gameStorage.Context();
        Account? account = db.GetAccount(username);
        // Create account if not exists.
        if (account == null) {
            account = new Account {
                Username = username,
                MachineId = machineId,
            };
            account = db.CreateAccount(account);
            if (account == null) {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to create account."));
            }
        } else {
#if !DEBUG
            if (account.Online) {
                return Task.FromResult(new LoginResponse {Code = LoginResponse.Types.Code.AlreadyLogin});
            }
#endif

            if (account.MachineId == default) {
                account.MachineId = machineId;
                db.UpdateAccount(account, true);
            } else if (account.MachineId != machineId) {
                return Task.FromResult(new LoginResponse {Code = LoginResponse.Types.Code.BlockNexonSn});
            }
        }

        return Task.FromResult(new LoginResponse{AccountId = account.Id});
    }
}
