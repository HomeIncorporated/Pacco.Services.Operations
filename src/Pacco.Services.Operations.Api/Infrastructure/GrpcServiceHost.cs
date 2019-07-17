using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Pacco.Services.Operations.Api.DTO;
using Pacco.Services.Operations.Api.Services;
using Services.Operations;

namespace Pacco.Services.Operations.Api.Infrastructure
{
    public class GrpcServiceHost : GrpcOperationsService.GrpcOperationsServiceBase
    {
        private readonly IOperationsService _operationsService;
        private readonly ILogger<GrpcServer> _logger;
        private readonly BlockingCollection<OperationDto> _operations = new BlockingCollection<OperationDto>();

        public GrpcServiceHost(IOperationsService operationsService, ILogger<GrpcServer> logger)
        {
            _operationsService = operationsService;
            _logger = logger;
            _operationsService.OperationUpdated += (s, e) => _operations.TryAdd(e.Operation);
        }

        public override async Task<GetOperationResponse> GetOperation(GetOperationRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation($"Received 'Get operation' (id: {request.Id}) request from: {context.Peer}");

            return Guid.TryParse(request.Id, out var id)
                ? Map(await _operationsService.GetAsync(id))
                : new GetOperationResponse();
        }

        public override async Task SubscribeOperations(Empty request,
            IServerStreamWriter<GetOperationResponse> responseStream, ServerCallContext context)
        {
            _logger.LogInformation($"Received 'Subscribe operations' request from: {context.Peer}");
            while (true)
            {
                var operation = _operations.Take();
                await responseStream.WriteAsync(Map(operation));
            }
        }

        private static GetOperationResponse Map(OperationDto operation)
            => operation is null
                ? new GetOperationResponse()
                : new GetOperationResponse
                {
                    Id = operation.Id.ToString("N"),
                    UserId = operation.UserId.ToString("N"),
                    Name = operation.Name,
                    Code = operation.Code,
                    Reason = operation.Reason,
                    State = operation.State.ToString().ToLowerInvariant()
                };
    }
}