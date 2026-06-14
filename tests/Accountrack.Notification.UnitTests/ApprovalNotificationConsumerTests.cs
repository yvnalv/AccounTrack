using Accountrack.Modules.Contracts.Events;
using Accountrack.Notification.Application;
using Accountrack.Notification.Application.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;
using NotificationEntity = Accountrack.Notification.Domain.Notification;

namespace Accountrack.Notification.UnitTests;

public class ApprovalNotificationConsumerTests
{
    private readonly INotificationRepository _repo = Substitute.For<INotificationRepository>();
    private readonly INotificationUnitOfWork _uow = Substitute.For<INotificationUnitOfWork>();
    private readonly Guid _submitter = Guid.NewGuid();

    private ApprovalNotificationConsumer Consumer() => new(_repo, _uow);

    [Fact]
    public async Task Decision_notifies_the_submitter()
    {
        NotificationEntity? captured = null;
        _repo.Add(Arg.Do<NotificationEntity>(n => captured = n));

        await Consumer().HandleAsync(
            new ApprovalDecided("PurchaseOrder", Guid.NewGuid(), Guid.NewGuid(), "Approved", _submitter, Guid.NewGuid(), true),
            CancellationToken.None);

        captured!.UserId.Should().Be(_submitter);
        captured.Title.Should().Contain("approved");
        captured.IsRead.Should().BeFalse();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejection_notifies_the_submitter()
    {
        NotificationEntity? captured = null;
        _repo.Add(Arg.Do<NotificationEntity>(n => captured = n));

        await Consumer().HandleAsync(
            new ApprovalDecided("Expense", Guid.NewGuid(), Guid.NewGuid(), "Rejected", _submitter, Guid.NewGuid(), false),
            CancellationToken.None);

        captured!.Title.Should().Contain("rejected");
    }

    [Fact]
    public async Task Submission_notifies_the_submitter_that_it_is_pending()
    {
        NotificationEntity? captured = null;
        _repo.Add(Arg.Do<NotificationEntity>(n => captured = n));

        await Consumer().HandleAsync(
            new ApprovalSubmitted("PurchaseOrder", Guid.NewGuid(), Guid.NewGuid(), "Pending", _submitter),
            CancellationToken.None);

        captured!.UserId.Should().Be(_submitter);
        captured.Body.Should().Contain("pending");
    }
}
