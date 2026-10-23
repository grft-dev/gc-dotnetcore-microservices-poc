using System.Threading;
using System.Threading.Tasks;
using ChatService.Api.Commands;
using MediatR;

namespace ChatService.Commands;

public class SendNotificationHandler : IRequestHandler<SendNotificationCommand, Unit>
{
    public async Task<Unit> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Unit.Value;
    }
}