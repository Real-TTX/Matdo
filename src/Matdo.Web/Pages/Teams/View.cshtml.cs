using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Teams;

public class TeamViewModel : PageModel
{
    private readonly TeamService _teams;
    private readonly ICurrentUserAccessor _me;

    public TeamViewModel(TeamService teams, ICurrentUserAccessor me)
    {
        _teams = teams;
        _me = me;
    }

    public Team Team { get; set; } = null!;
    public List<Invitation> Pending { get; set; } = new();
    public bool IsManager { get; set; }
    public bool IsOwner { get; set; }
    public long MeId { get; set; }
    public string? Message { get; set; }

    private async Task<bool> LoadAsync(long id)
    {
        var team = await _teams.GetAsync(id);
        if (team is null) return false;
        Team = team;
        MeId = _me.UserId ?? 0;
        IsManager = await _teams.IsManagerAsync(id);
        IsOwner = await _teams.IsOwnerAsync(id);
        Pending = await _teams.GetPendingInvitationsAsync(id);
        Message = TempData["TeamMsg"] as string;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id)
        => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostInviteAsync(long id, string? email, int role)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email)) return RedirectToPage(new { id });
            var o = await _teams.InviteToTeamAsync(id, email, (TeamRole)role);
            // Neutrale Einheitsmeldung: verrät nicht, ob zur Adresse schon ein Konto existiert.
            TempData["TeamMsg"] = o switch
            {
                TeamService.InviteOutcome.Self => "Du bist bereits im Team.",
                _ => "Eingeladen. Sobald die Person die Einladung annimmt, wird sie Mitglied."
            };
        }
        catch (InvalidOperationException ex)
        {
            TempData["TeamMsg"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoveMemberAsync(long id, long userId)
    {
        await _teams.RemoveMemberAsync(id, userId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(long id, long userId, int role)
    {
        await _teams.ChangeRoleAsync(id, userId, (TeamRole)role);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelInviteAsync(long id, long invitationId)
    {
        await _teams.CancelInvitationAsync(invitationId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRenameAsync(long id, string? name, string? color)
    {
        await _teams.UpdateAsync(id, name ?? "", color ?? "");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _teams.DeleteAsync(id);
        return Redirect("/Teams");
    }

    public async Task<IActionResult> OnPostLeaveAsync(long id)
    {
        await _teams.LeaveAsync(id);
        return Redirect("/Teams");
    }
}
