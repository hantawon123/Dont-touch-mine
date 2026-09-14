using Game.Client.Character;
using Game.Client.Home;
using Game.Client.Lobby;
using Game.Client.Settings;
using Game.Core.Ports;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class KickConfirmViewTests
    {
        [Test]
        public void Show_BuildsTheSettingsConfirmPlateWithoutASubtitle()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<KickConfirmView>();
                view.Show(KickConfirmView.FormatTitle("게스트닉"));

                var overlay = canvas.transform.Find(KickConfirmView.RootName);
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.gameObject.activeSelf, Is.True);
                Assert.That(overlay.Find("Panel/Subtitle"), Is.Null);

                var panel = overlay.Find("Panel") as RectTransform;
                Assert.That(panel.sizeDelta, Is.EqualTo(CharacterClosetStyle.Modal.PanelSize));
                Assert.That(
                    overlay.Find("Panel/" + KickConfirmView.ReasonRootName).gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    overlay.Find("Panel/" + KickConfirmView.NoteRootName).gameObject.activeSelf,
                    Is.False);

                var title = overlay.Find("Panel/Title").GetComponent<TMP_Text>();
                Assert.That(title.text, Is.EqualTo("게스트닉 님을\n강퇴하시겠습니까?"));
                Assert.That(title.fontSize, Is.EqualTo(CharacterClosetStyle.Modal.TitleFontSize));
                Assert.That(title.font, Is.EqualTo(HomeUiFonts.Apply()));

                var cancel = overlay.Find("Panel/DeclineButton/Label").GetComponent<TMP_Text>();
                var accept = overlay.Find("Panel/AcceptButton/Label").GetComponent<TMP_Text>();
                Assert.That(cancel.text, Is.EqualTo(KickConfirmView.CancelLabel));
                Assert.That(accept.text, Is.EqualTo(KickConfirmView.ConfirmLabel));
                Assert.That(
                    overlay.Find("Panel/DeclineButton").GetComponent<Image>().color,
                    Is.EqualTo(CharacterClosetStyle.Palette.DeclineFill));
                Assert.That(
                    overlay.Find("Panel/AcceptButton").GetComponent<Image>().color,
                    Is.EqualTo(CharacterClosetStyle.Palette.AcceptFill));
                Assert.That(overlay.Find("Panel/CloseButton"), Is.Not.Null);

                view.Hide();
                Assert.That(overlay.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ReasonLabel_UsesTheKoreanCopy()
        {
            Assert.That(KickConfirmView.ReasonLabel(ReportReason.Abuse), Is.EqualTo("욕설/비하"));
            Assert.That(KickConfirmView.ReasonLabel(ReportReason.Cheating), Is.EqualTo("치팅"));
            Assert.That(KickConfirmView.ReasonLabel(ReportReason.Spam), Is.EqualTo("도배/광고"));
            Assert.That(
                KickConfirmView.ReasonLabel(ReportReason.InappropriateName),
                Is.EqualTo("부적절한 닉네임"));
            Assert.That(KickConfirmView.ReasonLabel(ReportReason.Other), Is.EqualTo("기타"));
        }

        [Test]
        public void Show_Report_OffersEveryKoreanReasonAndKeepsTheChoice()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<KickConfirmView>();
                view.Show("게스트님을 신고하시겠습니까?", "확인", true);

                var overlay = canvas.transform.Find(KickConfirmView.RootName);
                var panel = overlay.Find("Panel") as RectTransform;
                Assert.That(panel.sizeDelta, Is.EqualTo(KickConfirmView.ReportPanelSize));

                var reason = overlay.Find("Panel/" + KickConfirmView.ReasonRootName);
                Assert.That(reason.gameObject.activeSelf, Is.True);
                Assert.That(view.SelectedReason, Is.EqualTo(ReportReason.Abuse));
                Assert.That(
                    reason.Find(KickConfirmView.ReasonFieldName + "/Value").GetComponent<TMP_Text>().text,
                    Is.EqualTo(KickConfirmView.ReasonLabel(ReportReason.Abuse)));

                var options = reason.Find(KickConfirmView.ReasonOptionsName);
                Assert.That(options.gameObject.activeSelf, Is.False);

                reason.Find(KickConfirmView.ReasonFieldName).GetComponent<Button>().onClick.Invoke();
                Assert.That(options.gameObject.activeSelf, Is.True);
                Assert.That(options.childCount, Is.EqualTo(KickConfirmView.Reasons.Length));

                for (var index = 0; index < KickConfirmView.Reasons.Length; index++)
                {
                    var reasonKind = KickConfirmView.Reasons[index];
                    var label = options.Find(reasonKind + "/Label").GetComponent<TMP_Text>();
                    Assert.That(label.text, Is.EqualTo(KickConfirmView.ReasonLabel(reasonKind)));
                }

                options.Find(ReportReason.Cheating.ToString()).GetComponent<Button>().onClick.Invoke();
                Assert.That(view.SelectedReason, Is.EqualTo(ReportReason.Cheating));
                Assert.That(
                    reason.Find(KickConfirmView.ReasonFieldName + "/Value").GetComponent<TMP_Text>().text,
                    Is.EqualTo("치팅"));
                Assert.That(options.gameObject.activeSelf, Is.False);

                var note = overlay.Find("Panel/" + KickConfirmView.NoteRootName) as RectTransform;
                Assert.That(note, Is.Not.Null);
                Assert.That(note.gameObject.activeSelf, Is.True);
                Assert.That(note.sizeDelta, Is.EqualTo(new Vector2(
                    KickConfirmView.NoteWidth,
                    KickConfirmView.NoteHeight)));
                var input = note.GetComponent<TMP_InputField>();
                Assert.That(input, Is.Not.Null);
                Assert.That(input.characterLimit, Is.EqualTo(KickConfirmView.NoteMaxLength));
                Assert.That(input.richText, Is.False);
                Assert.That(input.textComponent.richText, Is.False);
                Assert.That(input.lineType, Is.EqualTo(TMP_InputField.LineType.MultiLineNewline));
                var counter = note.Find(KickConfirmView.NoteCounterName) as RectTransform;
                Assert.That(counter, Is.Not.Null);
                Assert.That(counter.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(counter.pivot, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(
                    counter.GetComponent<TMP_Text>().text,
                    Is.EqualTo("0/" + KickConfirmView.NoteMaxLength));

                input.text = "욕설을 했습니다";
                Assert.That(view.Note, Is.EqualTo("욕설을 했습니다"));
                Assert.That(
                    counter.GetComponent<TMP_Text>().text,
                    Is.EqualTo("욕설을 했습니다".Length + "/" + KickConfirmView.NoteMaxLength));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Show_Report_OtherNeedsFiveCharactersToConfirm()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<KickConfirmView>();
                view.Show("게스트님을 신고하시겠습니까?", "확인", true);

                var overlay = canvas.transform.Find(KickConfirmView.RootName);
                var accept = overlay.Find("Panel/AcceptButton").GetComponent<Button>();
                var note = overlay.Find("Panel/" + KickConfirmView.NoteRootName)
                    .GetComponent<TMP_InputField>();
                Assert.That(accept.interactable, Is.True);

                overlay.Find("Panel/" + KickConfirmView.ReasonRootName + "/"
                    + KickConfirmView.ReasonFieldName).GetComponent<Button>().onClick.Invoke();
                overlay.Find("Panel/" + KickConfirmView.ReasonRootName + "/"
                    + KickConfirmView.ReasonOptionsName + "/"
                    + ReportReason.Other).GetComponent<Button>().onClick.Invoke();
                Assert.That(view.SelectedReason, Is.EqualTo(ReportReason.Other));
                Assert.That(accept.interactable, Is.False);
                Assert.That(
                    overlay.Find("Panel/AcceptButton").GetComponent<Image>().color,
                    Is.EqualTo(SettingsStyle.Palette.ButtonOffFill));

                note.text = "네글자임";
                Assert.That(note.text.Trim().Length, Is.EqualTo(4));
                Assert.That(accept.interactable, Is.False);

                note.text = "     ";
                Assert.That(accept.interactable, Is.False);

                note.text = "다섯글자임";
                Assert.That(note.text.Trim().Length, Is.EqualTo(5));
                Assert.That(accept.interactable, Is.True);
                Assert.That(
                    overlay.Find("Panel/AcceptButton").GetComponent<Image>().color,
                    Is.EqualTo(CharacterClosetStyle.Palette.AcceptFill));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Show_Kick_HidesTheReportNoteField()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = canvas.AddComponent<KickConfirmView>();
                view.Show("게스트님을 신고하시겠습니까?", "확인", true);
                view.Show(KickConfirmView.FormatTitle("게스트닉"));

                var note = canvas.transform.Find(
                    KickConfirmView.RootName + "/Panel/" + KickConfirmView.NoteRootName);
                Assert.That(note.gameObject.activeSelf, Is.False);
                Assert.That(view.Note, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
