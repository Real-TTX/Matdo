using System.ComponentModel.DataAnnotations;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Projects;

public class ProjectEditModel : PageModel
{
    private readonly ProjectService _projects;
    private readonly ShareService _shares;
    private readonly TeamService _teams;
    private readonly JsonConfigService _config;

    public ProjectEditModel(ProjectService projects, ShareService shares, TeamService teams, JsonConfigService config)
    {
        _projects = projects;
        _shares = shares;
        _teams = teams;
        _config = config;
    }

    public string? IcalFeedUrl { get; set; }

    private string BuildFeedUrl(Guid token) =>
        _config.Current.PublicBaseUrl.TrimEnd('/') + "/feed/project/" + token.ToString("N") + ".ics";

    [BindProperty] public InputModel Input { get; set; } = new();
    public bool IsNew => Input.Id == 0;
    public List<KanbanColumn> Columns { get; set; } = new();
    public List<ProjectShare> Shares { get; set; } = new();
    public List<User> ShareableUsers { get; set; } = new();
    public List<Project> ParentOptions { get; set; } = new();
    public List<Team> MyTeams { get; set; } = new();
    public List<Invitation> PendingInvites { get; set; } = new();
    public string? ShareMessage { get; set; }

    public static readonly string[] Palette =
        { "#8300bc", "#884dff", "#af38eb", "#dc4c3e", "#eb8909", "#f9d900", "#7ecc49", "#158fad", "#4073ff", "#808080" };

    public class InputModel
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Bitte einen Namen angeben.")]
        public string Name { get; set; } = "";
        [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Ungültige Farbe.")]
        public string Color { get; set; } = "#8300bc";
        public int ViewType { get; set; }
        public bool IsFavorite { get; set; }
        public long? ParentProjectId { get; set; }
        public long? TeamId { get; set; }
    }

    private async Task LoadAsync(long id)
    {
        Columns = await _projects.GetColumnsAsync(id);
        Shares = await _shares.GetProjectSharesAsync(id);
        ShareableUsers = await _shares.GetShareableUsersAsync();
        PendingInvites = await _shares.GetProjectPendingInvitesAsync(id);
        await LoadContextAsync(id);
    }

    /// <summary>Teams + Eltern-Projekte passend zum aktuellen Team-Kontext (Input.TeamId) laden.</summary>
    private async Task LoadContextAsync(long excludeId)
    {
        MyTeams = (await _teams.GetMyTeamsAsync()).Select(t => t.Team).ToList();
        // Nur Eltern aus demselben Kontext: gleiches Team bzw. persönliche Projekte (TeamId == null).
        ParentOptions = (await _projects.GetAllAsync())
            .Where(p => p.Id != excludeId && p.TeamId == Input.TeamId)
            .ToList();
    }

    public async Task<IActionResult> OnGetAsync(long? id, long? teamId)
    {
        ShareMessage = TempData["ShareMsg"] as string;
        if (id is > 0)
        {
            var p = await _projects.GetOwnedAsync(id.Value);
            if (p is null) return NotFound();
            Input = new InputModel { Id = p.Id, Name = p.Name, Color = p.Color, ViewType = (int)p.ViewType, IsFavorite = p.IsFavorite, ParentProjectId = p.ParentProjectId, TeamId = p.TeamId };
            if (p.IcalToken is Guid tok) IcalFeedUrl = BuildFeedUrl(tok);
            await LoadAsync(p.Id);
        }
        else
        {
            // Team ergibt sich aus dem Kontext (das „+" am jeweiligen Team bzw. an „Meine Projekte").
            MyTeams = (await _teams.GetMyTeamsAsync()).Select(t => t.Team).ToList();
            if (teamId is long tid && MyTeams.Any(t => t.Id == tid)) Input.TeamId = tid;
            await LoadContextAsync(0);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            if (Input.Id > 0) await LoadAsync(Input.Id);
            else await LoadContextAsync(0);
            return Page();
        }

        var project = new Project
        {
            Id = Input.Id,
            Name = Input.Name.Trim(),
            Color = Input.Color,
            ViewType = (ProjectViewType)Input.ViewType,
            IsFavorite = Input.IsFavorite,
            ParentProjectId = Input.ParentProjectId,
            TeamId = Input.TeamId
        };

        if (Input.Id == 0)
        {
            var created = await _projects.CreateAsync(project);
            return RedirectToPage(new { id = created.Id, saved = true });
        }

        await _projects.UpdateAsync(project);
        return RedirectToPage(new { id = Input.Id, saved = true });
    }

    public async Task<IActionResult> OnPostAddColumnAsync(long id, string columnName)
    {
        if (!string.IsNullOrWhiteSpace(columnName))
            await _projects.AddColumnAsync(id, columnName.Trim());
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRenameColumnAsync(long id, long columnId, string columnName)
    {
        if (!string.IsNullOrWhiteSpace(columnName))
            await _projects.RenameColumnAsync(columnId, columnName.Trim());
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteColumnAsync(long id, long columnId)
    {
        await _projects.DeleteColumnAsync(columnId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostShareAsync(long id, long targetUserId, int permission)
    {
        await _shares.ShareProjectAsync(id, targetUserId, (SharePermission)permission);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUnshareAsync(long id, long targetUserId)
    {
        await _shares.UnshareProjectAsync(id, targetUserId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostShareByEmailAsync(long id, string? shareEmail, int shareEmailPermission)
    {
        if (string.IsNullOrWhiteSpace(shareEmail)) return RedirectToPage(new { id });
        var outcome = await _shares.ShareProjectByEmailAsync(id, shareEmail, (SharePermission)shareEmailPermission);
        // Ergebnis über TempData zurückmelden (überlebt den Redirect).
        TempData["ShareMsg"] = outcome switch
        {
            ShareService.ShareByEmailOutcome.Shared => "Projekt freigegeben.",
            ShareService.ShareByEmailOutcome.AlreadyShared => "Freigabe aktualisiert.",
            ShareService.ShareByEmailOutcome.PendingInvite => "Einladung gespeichert – sie greift automatisch bei der Registrierung.",
            ShareService.ShareByEmailOutcome.Self => "Du kannst nicht mit dir selbst teilen.",
            _ => "Nur der Eigentümer kann teilen."
        };
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelInviteAsync(long id, long invitationId)
    {
        await _shares.CancelProjectInviteAsync(invitationId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostFeedAsync(long id)
    {
        await _projects.SetIcalTokenAsync(id);
        return RedirectToPage(new { id, saved = false });
    }

    public async Task<IActionResult> OnPostFeedDisableAsync(long id)
    {
        await _projects.ClearIcalTokenAsync(id);
        return RedirectToPage(new { id, saved = false });
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _projects.DeleteAsync(id);
        return Redirect("/Projects");
    }
}
