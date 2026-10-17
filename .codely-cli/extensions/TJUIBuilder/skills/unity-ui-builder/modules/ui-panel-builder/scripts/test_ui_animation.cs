// Internal regression test for TJUIBuilder library maintainers — NOT part of the ui-panel-builder skill pipeline.
// Run after modifying UIAnimationHelper (Runtime/Panel/UIAnimationHelper.cs) via execute_csharp_script.
// Self-contained; creates and destroys temp GameObjects.
//
// Edit-mode constraint: coroutine-driven progression (per-frame animation) and the
// legacy proxy-positive path (which calls MonoBehaviour.StartCoroutine) only run in
// Play Mode — those are covered by game-side panel regression. This script asserts
// the deterministic setup/contract surfaces:
//   T01  AttachButtonEffect setup: EventTrigger 4 entries + ButtonScaleRunner, scale untouched
//   T02  AttachButtonEffect idempotent: second call no-ops (still 4 entries, 1 runner)
//   T03  Legacy StaggerFadeIn on plain RectTransform container: completes, no CanvasGroup side effects
//   T04  Legacy StaggerEntrance on plain RectTransform container: completes, no CanvasGroup side effects
//   T05  Host overloads null guards: (null host, parent) / (host, null parent) complete without exception
//   T06  FadeIn(delay>0) first step: alpha==0 synchronously, yields WaitForSecondsRealtime
//   T07  PanelFadeIn first step: CanvasGroup auto-added on panel root, still running
//   T08  LogoBounceIn first step: scale snaps to ~0 (back-ease starts at zero)
//   T09  SmoothSlider first step: value stays at `from`
//   T10  ScoreRoll first step: label shows `from`
//   T11  IdleFloat(null)/TitleGlow(null): complete without exception (null guards)
//   T12  PopIn first step: scale snaps to ~0 (mild back-ease starts at zero)
//   T13  StaggerIn: LayoutGroup auto-detection — layout parent keeps anchoredPosition (alpha mode),
//        plain parent slides children to start pos; inactive children skipped
//   T14  AttachButtonEffects: batch attaches only missing buttons, idempotent (second pass = 0)
//   T15  SmoothSliderTo/ScoreRollTo: from self-derived from slider.value / label text
//   T16  ScoreRollTo parse failure (non-numeric text) falls back to from=0
//   T17  StaggerIn container with only inactive children completes immediately
//   T18  AttachButtonEffects includeInactive=true covers inactive buttons
//   T19  PanelFadeIn(null) completes without exception (null guard)
//   T20  UIPosBaseline: PanelFadeIn interrupted mid-slide → replay starts from same baseline (no drift);
//        natural end → baseline refreshes after design-time move
//   T21  UIPosBaseline: StaggerIn (slide mode) interrupted → replay from baseline
int passCount = 0;
int failCount = 0;
var created = new System.Collections.Generic.List<UnityEngine.GameObject>();

void Check(bool condition, string testName, string detail)
{
    if (condition) { passCount++; UnityEngine.Debug.Log($"[UiAnimTest] PASS {testName}"); }
    else { failCount++; UnityEngine.Debug.LogError($"[UiAnimTest] FAIL {testName} - {detail}"); }
}

UnityEngine.GameObject MakeGO(string name)
{
    var go = new UnityEngine.GameObject(name);
    go.AddComponent<UnityEngine.RectTransform>();
    created.Add(go);
    return go;
}

// ---------------------------------------------
// T01: AttachButtonEffect setup
// ---------------------------------------------
var btnGO = new UnityEngine.GameObject("T01_Button");
btnGO.AddComponent<UnityEngine.RectTransform>();
var btn = btnGO.AddComponent<UnityEngine.UI.Button>();
btnGO.transform.localScale = new UnityEngine.Vector3(1.5f, 1.5f, 1.5f);
created.Add(btnGO);
Create.GameFramework.UIAnimationHelper.AttachButtonEffect(btn);
var trig = btnGO.GetComponent<UnityEngine.EventSystems.EventTrigger>();
var runner = btnGO.GetComponent<Create.GameFramework.ButtonScaleRunner>();
Check(trig != null && trig.triggers.Count == 4, "T01a 4 EventTrigger entries", "entries=" + (trig != null ? trig.triggers.Count.ToString() : "null trigger"));
Check(runner != null, "T01b ButtonScaleRunner attached", "runner missing");
Check(btnGO.transform.localScale == new UnityEngine.Vector3(1.5f, 1.5f, 1.5f), "T01c scale untouched by setup", "scale=" + btnGO.transform.localScale);

// ---------------------------------------------
// T02: AttachButtonEffect idempotent
// ---------------------------------------------
Create.GameFramework.UIAnimationHelper.AttachButtonEffect(btn);
Check(trig.triggers.Count == 4, "T02a second call adds no entries", "entries=" + trig.triggers.Count);
Check(btnGO.GetComponents<Create.GameFramework.ButtonScaleRunner>().Length == 1, "T02b still exactly 1 runner", "runner count != 1");

// ---------------------------------------------
// T03/T04: legacy signatures on plain RectTransform container (proxy==null path)
// ---------------------------------------------
var plain = MakeGO("T03_PlainContainer");
var plainChild = new UnityEngine.GameObject("Child");
plainChild.AddComponent<UnityEngine.RectTransform>();
plainChild.transform.SetParent(plain.transform, false);
created.Add(plainChild);
var legacyFade = Create.GameFramework.UIAnimationHelper.StaggerFadeIn(plain.GetComponent<UnityEngine.RectTransform>(), 0.08f, 0.3f);
bool legacyFadeDone = !legacyFade.MoveNext();
Check(legacyFadeDone, "T03a legacy StaggerFadeIn completes on plain container", "enumerator did not complete on first step");
Check(plainChild.GetComponent<UnityEngine.CanvasGroup>() == null, "T03b no CanvasGroup side effects", "CanvasGroup was added");
var legacySlide = Create.GameFramework.UIAnimationHelper.StaggerEntrance(plain.GetComponent<UnityEngine.RectTransform>(), 0.08f, 0.3f, 40f);
bool legacySlideDone = !legacySlide.MoveNext();
Check(legacySlideDone, "T04a legacy StaggerEntrance completes on plain container", "enumerator did not complete on first step");
Check(plainChild.GetComponent<UnityEngine.CanvasGroup>() == null, "T04b no CanvasGroup side effects", "CanvasGroup was added");

// ---------------------------------------------
// T05: host overloads null guards
// ---------------------------------------------
var hostGO = MakeGO("T05_Host");
var host = hostGO.AddComponent<Create.GameFramework.ButtonScaleRunner>();
var hostParent = MakeGO("T05_Parent").GetComponent<UnityEngine.RectTransform>();
Check(!Create.GameFramework.UIAnimationHelper.StaggerFadeIn(null, hostParent).MoveNext(), "T05a StaggerFadeIn(null host) completes", "threw or kept running");
Check(!Create.GameFramework.UIAnimationHelper.StaggerFadeIn(host, null).MoveNext(), "T05b StaggerFadeIn(null parent) completes", "threw or kept running");
Check(!Create.GameFramework.UIAnimationHelper.StaggerEntrance(null, hostParent).MoveNext(), "T05c StaggerEntrance(null host) completes", "threw or kept running");
Check(!Create.GameFramework.UIAnimationHelper.StaggerEntrance(host, null).MoveNext(), "T05d StaggerEntrance(null parent) completes", "threw or kept running");

// ---------------------------------------------
// T06: FadeIn with delay — synchronous alpha=0 + yields WaitForSecondsRealtime
// ---------------------------------------------
var fadeGO = MakeGO("T06_Target");
var fadeCG = fadeGO.AddComponent<UnityEngine.CanvasGroup>();
var fadeEnum = Create.GameFramework.UIAnimationHelper.FadeIn(fadeCG, 0.3f, 0.1f);
bool fadeRunning = fadeEnum.MoveNext();
Check(fadeRunning, "T06a FadeIn still running after first step", "completed early");
Check(UnityEngine.Mathf.Approximately(fadeCG.alpha, 0f), "T06b alpha==0 set synchronously", "alpha=" + fadeCG.alpha);
Check(fadeEnum.Current is UnityEngine.WaitForSecondsRealtime, "T06c yields WaitForSecondsRealtime", "Current=" + fadeEnum.Current);

// ---------------------------------------------
// T07: PanelFadeIn — CanvasGroup auto-added, still running
// ---------------------------------------------
var panelGO = MakeGO("T07_Panel");
var panelEnum = Create.GameFramework.UIAnimationHelper.PanelFadeIn(panelGO.transform, 0.35f, 30f);
bool panelRunning = panelEnum.MoveNext();
Check(panelRunning, "T07a PanelFadeIn still running after first step", "completed early");
Check(panelGO.GetComponent<UnityEngine.CanvasGroup>() != null, "T07b CanvasGroup auto-added", "CanvasGroup missing");

// ---------------------------------------------
// T08: LogoBounceIn — scale snaps to ~0 on first step (back-ease starts at zero)
// ---------------------------------------------
var logoGO = MakeGO("T08_Logo");
logoGO.transform.localScale = new UnityEngine.Vector3(2f, 2f, 2f);
var bounceEnum = Create.GameFramework.UIAnimationHelper.LogoBounceIn(logoGO.transform, 0.6f);
bool bounceRunning = bounceEnum.MoveNext();
float finalSqr = new UnityEngine.Vector3(2f, 2f, 2f).sqrMagnitude;
Check(bounceRunning, "T08a LogoBounceIn still running after first step", "completed early");
Check(logoGO.transform.localScale.sqrMagnitude < finalSqr * 0.01f, "T08b scale snapped to ~0", "scale=" + logoGO.transform.localScale);

// ---------------------------------------------
// T09: SmoothSlider — value stays at `from` after first step
// ---------------------------------------------
var sliderGO = MakeGO("T09_Slider");
var slider = sliderGO.AddComponent<UnityEngine.UI.Slider>();
var sliderEnum = Create.GameFramework.UIAnimationHelper.SmoothSlider(slider, 10f, 100f, 0.4f);
bool sliderRunning = sliderEnum.MoveNext();
Check(sliderRunning, "T09a SmoothSlider still running after first step", "completed early");
Check(UnityEngine.Mathf.Abs(slider.value - 10f) < 1f, "T09b value stays at from", "value=" + slider.value);

// ---------------------------------------------
// T10: ScoreRoll — label shows `from` after first step
// ---------------------------------------------
var labelGO = MakeGO("T10_Label");
var label = labelGO.AddComponent<UnityEngine.UI.Text>();
var rollEnum = Create.GameFramework.UIAnimationHelper.ScoreRoll(label, 100, 200, 0.8f);
bool rollRunning = rollEnum.MoveNext();
Check(rollRunning, "T10a ScoreRoll still running after first step", "completed early");
Check(label.text == "100", "T10b label shows from", "text=" + label.text);

// ---------------------------------------------
// T11: null guards on infinite loops
// ---------------------------------------------
Check(!Create.GameFramework.UIAnimationHelper.IdleFloat(null).MoveNext(), "T11a IdleFloat(null) completes", "threw or kept running");
Check(!Create.GameFramework.UIAnimationHelper.TitleGlow(null, UnityEngine.Color.white).MoveNext(), "T11b TitleGlow(null) completes", "threw or kept running");

// ---------------------------------------------
// T12: PopIn — scale snaps to ~0 on first step (mild back-ease starts at zero)
// ---------------------------------------------
var popGO = MakeGO("T12_Popup");
popGO.transform.localScale = new UnityEngine.Vector3(1f, 1f, 1f);
var popEnum = Create.GameFramework.UIAnimationHelper.PopIn(popGO.transform, 0.3f);
bool popRunning = popEnum.MoveNext();
Check(popRunning, "T12a PopIn still running after first step", "completed early");
Check(popGO.transform.localScale.sqrMagnitude < 1f * 0.01f, "T12b scale snapped to ~0", "scale=" + popGO.transform.localScale);

// ---------------------------------------------
// T13: StaggerIn — LayoutGroup auto-detection (single coroutine, no host param)
// ---------------------------------------------
var lgParent = MakeGO("T13_LayoutParent");
lgParent.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
var lgChild = MakeGO("T13_LayoutChild");
lgChild.transform.SetParent(lgParent.transform, false);
lgChild.GetComponent<UnityEngine.RectTransform>().anchoredPosition = new UnityEngine.Vector2(10f, 20f);
var lgEnum = Create.GameFramework.UIAnimationHelper.StaggerIn(lgParent.GetComponent<UnityEngine.RectTransform>(), 0.08f, 0.3f, 40f);
bool lgRunning = lgEnum.MoveNext();
var lgCG = lgChild.GetComponent<UnityEngine.CanvasGroup>();
Check(lgRunning, "T13a StaggerIn(layout parent) still running after first step", "completed early");
Check(lgCG != null && lgCG.alpha < 0.01f, "T13b alpha==0 set synchronously", "cg=" + (lgCG != null ? lgCG.alpha.ToString() : "missing"));
Check(UnityEngine.Vector2.Distance(lgChild.GetComponent<UnityEngine.RectTransform>().anchoredPosition, new UnityEngine.Vector2(10f, 20f)) < 0.01f,
    "T13c anchoredPosition untouched (alpha mode)", "pos=" + lgChild.GetComponent<UnityEngine.RectTransform>().anchoredPosition);

var slideParent = MakeGO("T13_SlideParent");
var slideChild = MakeGO("T13_SlideChild");
slideChild.transform.SetParent(slideParent.transform, false);
var slideRT = slideChild.GetComponent<UnityEngine.RectTransform>();
slideRT.anchoredPosition = new UnityEngine.Vector2(10f, 20f);
var slideEnum = Create.GameFramework.UIAnimationHelper.StaggerIn(slideParent.GetComponent<UnityEngine.RectTransform>(), 0.08f, 0.3f, 40f);
bool slideRunning = slideEnum.MoveNext();
Check(slideRunning, "T13d StaggerIn(plain parent) still running after first step", "completed early");
Check(UnityEngine.Vector2.Distance(slideRT.anchoredPosition, new UnityEngine.Vector2(10f, -20f)) < 0.01f,
    "T13e child slid to start pos (slide mode)", "pos=" + slideRT.anchoredPosition);

var inactiveChild = MakeGO("T13_Inactive");
inactiveChild.transform.SetParent(slideParent.transform, false);
inactiveChild.SetActive(false);
var skipEnum = Create.GameFramework.UIAnimationHelper.StaggerIn(slideParent.GetComponent<UnityEngine.RectTransform>(), 0.08f, 0.3f, 40f);
skipEnum.MoveNext();
Check(inactiveChild.GetComponent<UnityEngine.CanvasGroup>() == null, "T13f inactive child skipped", "CanvasGroup added to inactive child");

// ---------------------------------------------
// T14: AttachButtonEffects — batch, idempotent
// ---------------------------------------------
var batchRoot = MakeGO("T14_Root");
var btnAGO = new UnityEngine.GameObject("BtnA");
btnAGO.AddComponent<UnityEngine.RectTransform>();
var btnA = btnAGO.AddComponent<UnityEngine.UI.Button>();
btnAGO.transform.SetParent(batchRoot.transform, false);
created.Add(btnAGO);
var btnBGO = new UnityEngine.GameObject("BtnB");
btnBGO.AddComponent<UnityEngine.RectTransform>();
var btnB = btnBGO.AddComponent<UnityEngine.UI.Button>();
btnBGO.transform.SetParent(batchRoot.transform, false);
created.Add(btnBGO);
Create.GameFramework.UIAnimationHelper.AttachButtonEffect(btnB);   // pre-attached
int newlyAttached = Create.GameFramework.UIAnimationHelper.AttachButtonEffects(batchRoot.transform);
Check(newlyAttached == 1, "T14a batch attaches only missing buttons", "attached=" + newlyAttached);
Check(btnA.GetComponent<Create.GameFramework.ButtonScaleRunner>() != null, "T14b BtnA got effect", "runner missing");
var trigA = btnAGO.GetComponent<UnityEngine.EventSystems.EventTrigger>();
var trigB = btnBGO.GetComponent<UnityEngine.EventSystems.EventTrigger>();
Check(trigA != null && trigA.triggers.Count == 4, "T14c BtnA has 4 entries", "entries=" + (trigA != null ? trigA.triggers.Count.ToString() : "null"));
Check(trigB != null && trigB.triggers.Count == 4, "T14d BtnB not double-attached", "entries=" + (trigB != null ? trigB.triggers.Count.ToString() : "null"));
int secondPass = Create.GameFramework.UIAnimationHelper.AttachButtonEffects(batchRoot.transform);
Check(secondPass == 0, "T14e second batch call is a no-op", "attached=" + secondPass);

// ---------------------------------------------
// T15: SmoothSliderTo / ScoreRollTo — from self-derived
// ---------------------------------------------
var sliderToGO = MakeGO("T15_Slider");
var sliderTo = sliderToGO.AddComponent<UnityEngine.UI.Slider>();
sliderTo.value = 10f;
var stEnum = Create.GameFramework.UIAnimationHelper.SmoothSliderTo(sliderTo, 100f, 0.4f);
bool stRunning = stEnum.MoveNext();
Check(stRunning && UnityEngine.Mathf.Abs(sliderTo.value - 10f) < 1f, "T15a SmoothSliderTo starts from slider.value", "value=" + sliderTo.value);
var labelToGO = MakeGO("T15_Label");
var labelTo = labelToGO.AddComponent<UnityEngine.UI.Text>();
labelTo.text = "999";
var srEnum = Create.GameFramework.UIAnimationHelper.ScoreRollTo(labelTo, 200, 0.8f);
bool srRunning = srEnum.MoveNext();
Check(srRunning && labelTo.text == "999", "T15b ScoreRollTo starts from label's current value", "text=" + labelTo.text);

// ---------------------------------------------
// T16: ScoreRollTo parse failure falls back to 0
// ---------------------------------------------
var prGO = MakeGO("T16_ParseRoll");
var prLabel = prGO.AddComponent<UnityEngine.UI.Text>();
prLabel.text = "AMMO 99";
var prEnum = Create.GameFramework.UIAnimationHelper.ScoreRollTo(prLabel, 50, 0.5f);
bool prRunning = prEnum.MoveNext();
Check(prRunning && prLabel.text == "0", "T16a ScoreRollTo parse failure starts from 0", "running=" + prRunning + " text=" + prLabel.text);

// ---------------------------------------------
// T17: StaggerIn with all children inactive completes immediately
// ---------------------------------------------
var allInactive = MakeGO("T17_AllInactive");
var inactiveA = MakeGO("T17_InactiveA");
inactiveA.transform.SetParent(allInactive.transform, false);
inactiveA.SetActive(false);
var inactiveB = MakeGO("T17_InactiveB");
inactiveB.transform.SetParent(allInactive.transform, false);
inactiveB.SetActive(false);
Check(!Create.GameFramework.UIAnimationHelper.StaggerIn(allInactive.GetComponent<UnityEngine.RectTransform>()).MoveNext(),
    "T17a all-inactive container completes on first step", "did not complete");

// ---------------------------------------------
// T18: AttachButtonEffects includeInactive covers inactive buttons
// ---------------------------------------------
var incRoot = MakeGO("T18_Root");
var activeBtnGO = new UnityEngine.GameObject("T18_ActiveBtn");
activeBtnGO.AddComponent<UnityEngine.RectTransform>();
activeBtnGO.AddComponent<UnityEngine.UI.Button>();
activeBtnGO.transform.SetParent(incRoot.transform, false);
created.Add(activeBtnGO);
var inactBtnGO = new UnityEngine.GameObject("T18_InactiveBtn");
inactBtnGO.AddComponent<UnityEngine.RectTransform>();
inactBtnGO.AddComponent<UnityEngine.UI.Button>();
inactBtnGO.transform.SetParent(incRoot.transform, false);
inactBtnGO.SetActive(false);
created.Add(inactBtnGO);
int incAttached = Create.GameFramework.UIAnimationHelper.AttachButtonEffects(incRoot.transform, true);
Check(incAttached == 2, "T18a includeInactive attaches both buttons", "attached=" + incAttached);
Check(inactBtnGO.GetComponent<Create.GameFramework.ButtonScaleRunner>() != null, "T18b inactive button got effect", "runner missing");

// ---------------------------------------------
// T19: PanelFadeIn(null) completes without exception
// ---------------------------------------------
Check(!Create.GameFramework.UIAnimationHelper.PanelFadeIn(null).MoveNext(), "T19a PanelFadeIn(null) completes", "threw or kept running");

// ---------------------------------------------
// T20: UIPosBaseline — PanelFadeIn interruption drift recovery
// ---------------------------------------------
var blGO = MakeGO("T20_Baseline");
var blRT = blGO.GetComponent<UnityEngine.RectTransform>();
blRT.anchoredPosition = new UnityEngine.Vector2(10f, 20f);
var blRun1 = Create.GameFramework.UIAnimationHelper.PanelFadeIn(blRT, 0.35f, 30f);
blRun1.MoveNext();   // 模拟中断：首帧后丢弃 enumerator（StopAllCoroutines 语义）
var blRun2 = Create.GameFramework.UIAnimationHelper.PanelFadeIn(blRT, 0.35f, 30f);
blRun2.MoveNext();
Check(UnityEngine.Vector2.Distance(blRT.anchoredPosition, new UnityEngine.Vector2(10f, -10f)) < 0.01f,
    "T20a replay starts from same baseline (no drift)", "pos=" + blRT.anchoredPosition);
Create.GameFramework.UIPosBaseline.EndSlide(blRT);   // 模拟自然结束
blRT.anchoredPosition = new UnityEngine.Vector2(99f, 88f);   // 设计期移动
var blRun3 = Create.GameFramework.UIAnimationHelper.PanelFadeIn(blRT, 0.35f, 30f);
blRun3.MoveNext();
Check(UnityEngine.Vector2.Distance(blRT.anchoredPosition, new UnityEngine.Vector2(99f, 58f)) < 0.01f,
    "T20b clean start refreshes baseline after design move", "pos=" + blRT.anchoredPosition);
Create.GameFramework.UIPosBaseline.EndSlide(blRT);

// ---------------------------------------------
// T21: UIPosBaseline — StaggerIn (slide mode) interruption recovery
// ---------------------------------------------
var sgParent = MakeGO("T21_SgParent");   // 无 LayoutGroup → 滑入分支
var sgChild = MakeGO("T21_SgChild");
sgChild.transform.SetParent(sgParent.transform, false);
sgChild.GetComponent<UnityEngine.RectTransform>().anchoredPosition = new UnityEngine.Vector2(5f, 40f);
var sgRun1 = Create.GameFramework.UIAnimationHelper.StaggerIn(sgParent.GetComponent<UnityEngine.RectTransform>(), 0.08f, 0.3f, 30f);
sgRun1.MoveNext();   // 中断
var sgRun2 = Create.GameFramework.UIAnimationHelper.StaggerIn(sgParent.GetComponent<UnityEngine.RectTransform>(), 0.08f, 0.3f, 30f);
sgRun2.MoveNext();
Check(UnityEngine.Vector2.Distance(sgChild.GetComponent<UnityEngine.RectTransform>().anchoredPosition, new UnityEngine.Vector2(5f, 10f)) < 0.01f,
    "T21a replay starts from same baseline (no drift)", "pos=" + sgChild.GetComponent<UnityEngine.RectTransform>().anchoredPosition);
Create.GameFramework.UIPosBaseline.EndSlide(sgChild.GetComponent<UnityEngine.RectTransform>());

// ---------------------------------------------
// Cleanup
// ---------------------------------------------
foreach (var go in created)
    if (go != null) UnityEngine.Object.DestroyImmediate(go);

UnityEngine.Debug.Log($"[UiAnimTest] ===== RESULT: {passCount} passed, {failCount} failed =====");
if (failCount > 0)
    UnityEngine.Debug.LogError($"[UiAnimTest] UI_ANIMATION_TEST_FAILED pass={passCount} fail={failCount}");
