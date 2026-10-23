using ChatService.Api.Commands;
using MediatR;

namespace ChatService.Graft;

public class NotificationGraftService
{
    private readonly IMediator mediator;

    public NotificationGraftService(IMediator mediator)
    {
        this.mediator = mediator;
    }

    public bool SendNotification(SendNotificationCommand command)
    {
        mediator.Send(command).GetAwaiter().GetResult();
        return true;
    }
}
