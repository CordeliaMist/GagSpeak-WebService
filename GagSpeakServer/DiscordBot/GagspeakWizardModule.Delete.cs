using Discord.Interactions;
using Discord;

namespace GagspeakServer.Discord;

public partial class GagspeakWizardModule
{
    [ComponentInteraction("wizard-delete")]
    public async Task ComponentDelete()
    {
        if (!(await ValidateInteraction().ConfigureAwait(false))) return;

        _logger.LogInformation("{method}:{userId}", nameof(ComponentDelete), Context.Interaction.User.Id);

        using var gagspeakDb = GetDbContext();
        EmbedBuilder eb = new();
        eb.WithTitle("Delete Account");
        eb.WithDescription("You can delete your primary or secondary UIDs here." + Environment.NewLine + Environment.NewLine
            + "__Note: deleting your primary UID will delete all associated secondary UIDs as well.__" + Environment.NewLine + Environment.NewLine
            + "- 1️⃣ is your primary account/UID" + Environment.NewLine
            + "- 2️⃣ are all your secondary accounts/UIDs" + Environment.NewLine
            + "If you are using Vanity UIDs the original UID is displayed in the second line of the account selection.");
        eb.WithColor(Color.Blue);

        ComponentBuilder cb = new();
        await AddUserSelection(gagspeakDb, cb, "wizard-delete-select").ConfigureAwait(false);
        AddHome(cb);
        await ModifyInteraction(eb, cb).ConfigureAwait(false);
    }

    [ComponentInteraction("wizard-delete-select")]
    public async Task SelectionDeleteAccount(string uid)
    {
        if (!(await ValidateInteraction().ConfigureAwait(false))) return;

        _logger.LogInformation("{method}:{userId}:{uid}", nameof(SelectionDeleteAccount), Context.Interaction.User.Id, uid);

        using var gagspeakDb = GetDbContext();
        bool isPrimary = gagspeakDb.Auth.Single(u => u.UserUID == uid).PrimaryUserUID == null;
        EmbedBuilder eb = new();
        eb.WithTitle($"Are you sure you want to delete {uid}?");
        eb.WithDescription($"This operation is irreversible. All your pairs, joined syncshells and information stored on the service for {uid} will be " +
            $"irrevocably deleted." +
            (isPrimary ? (Environment.NewLine + Environment.NewLine +
            "⚠️ **You are about to delete a Primary UID, all attached Secondary UIDs and their information will be deleted as well.** ⚠️") : string.Empty));
        eb.WithColor(Color.Purple);
        ComponentBuilder cb = new();
        cb.WithButton("Cancel", "wizard-delete", emote: new Emoji("❌"));
        cb.WithButton($"Delete {uid}", "wizard-delete-confirm:" + uid, ButtonStyle.Danger, emote: new Emoji("🗑️"));
        await ModifyInteraction(eb, cb).ConfigureAwait(false);
    }

    [ComponentInteraction("wizard-delete-confirm:*")]
    public async Task ComponentDeleteAccountConfirm(string uid)
    {
        if (!(await ValidateInteraction().ConfigureAwait(false))) return;

        _logger.LogInformation("{method}:{userId}:{uid}", nameof(ComponentDeleteAccountConfirm), Context.Interaction.User.Id, uid);

        await RespondWithModalAsync<ConfirmDeletionModal>("wizard-delete-confirm-modal:" + uid).ConfigureAwait(false);
    }

    [ModalInteraction("wizard-delete-confirm-modal:*")]
    public async Task ModalDeleteAccountConfirm(string uid, ConfirmDeletionModal modal)
    {
        if (!(await ValidateInteraction().ConfigureAwait(false))) return;

        _logger.LogInformation("{method}:{userId}:{uid}", nameof(ModalDeleteAccountConfirm), Context.Interaction.User.Id, uid);

        try
        {
            if (!string.Equals("DELETE", modal.Delete, StringComparison.Ordinal))
            {
                EmbedBuilder eb = new();
                eb.WithTitle("Did not confirm properly");
                eb.WithDescription($"You entered {modal.Delete} but requested was DELETE. Please try again and enter DELETE to confirm.");
                eb.WithColor(Color.Red);
                ComponentBuilder cb = new();
                cb.WithButton("Cancel", "wizard-delete", emote: new Emoji("❌"));
                cb.WithButton("Retry", "wizard-delete-confirm:" + uid, emote: new Emoji("🔁"));

                await ModifyModalInteraction(eb, cb).ConfigureAwait(false);
            }
            else
            {

                using var db = GetDbContext();
                var user = db.Users.Single(u => u.UID == uid);

                EmbedBuilder eb = new();
                eb.WithTitle($"Account {uid} successfully deleted");
                eb.WithColor(Color.Green);
                ComponentBuilder cb = new();
                AddHome(cb);

                await ModifyModalInteraction(eb, cb).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling modal delete account confirm");
        }
    }
}
