using System;
using System.Diagnostics;
using Game.Client.Match;
using Game.Core.Lobby;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Game.Tests.EditMode {
 public sealed class FpsOptimizationRegressionTests {
  [Test] public void Chat_RepeatedMetricsPreserveRowsAndObserveFontMessageAndVisibilityChanges() {
   var root=new GameObject("Chat regression",typeof(RectTransform),typeof(Canvas));
   try {
    var view=MatchChatView.Create(root.transform,keepChromeVisible:true); view.SetMode(MatchChatHudMode.Full);
    view.SetMessages(new[]{new LobbyChatMessage("a","이름","채팅 표시 확인")});
    var row=view.transform.Find("HistoryPanel/Items/Row0"); var body=row.Find("Body").GetComponent<TMP_Text>();
    Canvas.ForceUpdateCanvases();
    for(int i=0;i<10;i++) view.ApplyListMetrics(1);
    var original=row.GetComponent<LayoutElement>().preferredHeight;
    var watch=Stopwatch.StartNew(); long bytes=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<200;i++) view.ApplyListMetrics(1);
    long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes; watch.Stop();
    TestContext.WriteLine($"CHAT_METRICS calls=200 ms={watch.Elapsed.TotalMilliseconds} bytes={allocated}");
    Assert.That(row.gameObject.activeSelf,Is.True); Assert.That(body.text,Is.EqualTo("채팅 표시 확인"));
    Assert.That(row.GetComponent<LayoutElement>().preferredHeight,Is.EqualTo(original));
    body.fontSize=72; view.ApplyListMetrics(1);
    Assert.That(row.GetComponent<LayoutElement>().preferredHeight,Is.GreaterThan(original));
    view.gameObject.SetActive(false); view.gameObject.SetActive(true); view.ApplyListMetrics(1.15f);
    Assert.That(row.gameObject.activeInHierarchy,Is.True);
    view.SetMessages(new[]{new LobbyChatMessage("b","바뀐 이름","첫 줄\n둘째 줄\n셋째 줄")}); view.ApplyListMetrics(.85f);
    Assert.That(body.text,Does.Contain("셋째 줄")); Assert.That(row.Find("Name").GetComponent<TMP_Text>().text,Is.EqualTo("바뀐 이름"));
    Assert.That(row.GetComponent<LayoutElement>().preferredHeight,Is.GreaterThan(original));
    view.SetMessages(Array.Empty<LobbyChatMessage>());view.ApplyListMetrics(.85f);Assert.That(row.gameObject.activeSelf,Is.False);
    view.SetMessages(new[]{new LobbyChatMessage("c","재표시","복원")});view.ApplyListMetrics(1);Assert.That(row.gameObject.activeSelf,Is.True);
   } finally { Object.DestroyImmediate(root); }
  }
 }
}
