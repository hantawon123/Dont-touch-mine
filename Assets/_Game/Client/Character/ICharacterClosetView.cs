using System;
using System.Collections.Generic;
using Game.Core.Players;
using Game.Core.Settings;

namespace Game.Client.Character
{
    /// <summary>
    /// The closet screen, as the presenter needs it: four things it can be
    /// asked to draw and three things the player can do on it.
    /// </summary>
    public interface ICharacterClosetView
    {
        /// <summary>
        /// The screen became visible, including a cached overlay reopening.
        /// </summary>
        event Action Opened;

        /// <summary>The arrow at the top left.</summary>
        event Action BackRequested;

        event Action ResetRequested;

        event Action ApplyRequested;

        /// <summary>The 예 of whichever confirmation is up.</summary>
        event Action ConfirmAccepted;

        /// <summary>Its 아니오, its X, or Escape.</summary>
        event Action ConfirmDismissed;

        event Action<AvatarPartCategory> CategorySelected;

        /// <summary>
        /// A cell in the grid, carrying the part it stands for. An empty id is
        /// the first cell, which takes the part off.
        /// </summary>
        event Action<AvatarPartCategory, string> PartSelected;

        /// <summary>Fills the rail down the left. Called once.</summary>
        void ShowCategories(IReadOnlyList<AvatarPartGroup> groups);

        /// <summary>
        /// Puts a category's parts in the locker and marks its tab as the one
        /// being looked at.
        /// </summary>
        void ShowParts(AvatarPartGroup group, string selectedPartId);

        /// <summary>
        /// Moves the selection ring without rebuilding the grid, for the common
        /// case of picking within the category already shown.
        /// </summary>
        void ShowSelectedPart(AvatarPartCategory category, string partId);

        /// <summary>Dresses the character standing in the middle.</summary>
        void ShowPreview(AvatarAppearance appearance);

        /// <summary>
        /// Turns the two buttons under the character on or off, which is the
        /// screen's whole account of whether anything has been changed.
        /// </summary>
        void SetActionsEnabled(bool enabled);

        /// <summary>
        /// Says that what was applied did not reach the account. An empty
        /// message says nothing.
        /// </summary>
        void ShowSaveError(string message);

        void ShowConfirm(ClosetConfirmKind kind);

        void HideConfirm();

        void ShowChrome(UiLocale locale);
    }
}
