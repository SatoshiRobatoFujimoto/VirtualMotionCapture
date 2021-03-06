﻿using ControlWindow;
using RootMotion.FinalIK;
using sh_akira;
using sh_akira.OVRTracking;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using UnityEngine;
using VRM;

public class ControlWinFormsWindow : MonoBehaviour
{
    private WindowLoader win;

    public TrackerHandler handler = null;

    public CameraLookTarget CalibrationCamera;

    public Renderer BackgroundRenderer;

    public GameObject GridCanvas;

    public DynamicOVRLipSync LipSync;

    public BlinkController blinkController;

    private GameObject CurrentModel = null;

    private VRIK vrik = null;

    private Camera currentCamera = null;
    private CameraLookTarget currentCameraLookTarget = null;

    private enum MouseButtons
    {
        Left = 0,
        Right = 1,
        Center = 2,
    }

    #region WindowsAPI

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmMargin
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);
    public static Vector2 GetWindowsMousePosition()
    {
        POINT pos;
        if (GetCursorPos(out pos)) return new Vector2(pos.X, pos.Y);
        return Vector2.zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string className, string windowName);
    private static IntPtr GetUnityWindowHandle() => FindWindow(null, Application.productName);

    private uint defaultWindowStyle;
    private uint defaultExWindowStyle;

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong); /*x uint o int unchecked*/
    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private static readonly IntPtr HWND_TOP = new IntPtr(0);
    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    [Flags()]
    private enum SetWindowPosFlags : uint
    {
        AsynchronousWindowPosition = 0x4000,
        DeferErase = 0x2000,
        DrawFrame = 0x0020,
        FrameChanged = 0x0020,
        HideWindow = 0x0080,
        DoNotActivate = 0x0010,
        DoNotCopyBits = 0x0100,
        IgnoreMove = 0x0002,
        DoNotChangeOwnerZOrder = 0x0200,
        DoNotRedraw = 0x0008,
        DoNotReposition = 0x0200,
        DoNotSendChangingEvent = 0x0400,
        IgnoreResize = 0x0001,
        IgnoreZOrder = 0x0004,
        ShowWindow = 0x0040,
        NoFlag = 0x0000,
        IgnoreMoveAndResize = IgnoreMove | IgnoreResize,
    }
    private static RECT GetUnityWindowPosition() { RECT r; GetWindowRect(GetUnityWindowHandle(), out r); return r; }
    private static void SetUnityWindowPosition(int x, int y) => SetWindowPos(GetUnityWindowHandle(), IntPtr.Zero, x, y, 0, 0, SetWindowPosFlags.IgnoreResize);
    private static void SetUnityWindowSize(int width, int height) => SetWindowPos(GetUnityWindowHandle(), IntPtr.Zero, 0, 0, width, height, SetWindowPosFlags.IgnoreMove);
    private static void SetUnityWindowTopMost(bool enable) => SetWindowPos(GetUnityWindowHandle(), enable ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SetWindowPosFlags.IgnoreMoveAndResize);

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref DwmMargin margins);
    private static void SetDwmTransparent(bool enable)
    {
        var margins = new DwmMargin() { cxLeftWidth = enable ? -1 : 0 };
        DwmExtendFrameIntoClientArea(GetUnityWindowHandle(), ref margins);
    }

    const int GWL_STYLE = -16;
    const uint WS_POPUP = 0x80000000;
    const uint WS_VISIBLE = 0x10000000;
    const int GWL_EXSTYLE = -20;
    const uint WS_EX_LAYERED = 0x00080000;
    const uint WS_EX_TRANSPARENT = 0x00000020;
    #endregion

    // Use this for initialization
    void Start()
    {
        win = WindowLoader.Instance;
        win.LoadVRM = LoadVRM;
        win.ImportVRM = ImportVRM;

        win.Calibrate = Calibrate;
        win.EndCalibrate = EndCalibrate;

        win.SetLipSyncEnable = SetLipSyncEnable;
        win.GetLipSyncDevices = GetLipSyncDevices;
        win.SetLipSyncDevice = SetLipSyncDevice;
        win.SetLipSyncGain = SetLipSyncGain;
        win.SetLipSyncMaxWeightEnable = SetLipSyncMaxWeightEnable;
        win.SetLipSyncWeightThreashold = SetLipSyncWeightThreashold;
        win.SetLipSyncMaxWeightEmphasis = SetLipSyncMaxWeightEmphasis;

        win.ChangeBackgroundColor = ChangeBackgroundColor;
        win.SetBackgroundTransparent = SetBackgroundTransparent;
        win.SetWindowBorder = SetWindowBorder;
        win.SetWindowTopMost = SetWindowTopMost;
        win.SetWindowClickThrough = SetWindowClickThrough;

        win.ChangeCamera = ChangeCamera;
        win.SetGridVisible = SetGridVisible;

        win.SetAutoBlinkEnable = SetAutoBlinkEnable;
        win.SetBlinkTimeMin = SetBlinkTimeMin;
        win.SetBlinkTimeMax = SetBlinkTimeMax;
        win.SetCloseAnimationTime = SetCloseAnimationTime;
        win.SetOpenAnimationTime = SetOpenAnimationTime;
        win.SetClosingTime = SetClosingTime;
        win.SetDefaultFace = SetDefaultFace;

        win.LoadSettings = LoadSettings;
        win.SaveSettings = SaveSettings;

        win.RunAfterMs = RunAfterMs;
        win.RunOnUnity = RunOnUnity;

        win.TestEvent = TestEvent;
        win.ShowWindow();

        CurrentSettings.BackgroundColor = BackgroundRenderer.material.color;
        CurrentSettings.CustomBackgroundColor = BackgroundRenderer.material.color;
    }

    #region VRM

    private VRMData LoadVRM()
    {
        var path = WindowsDialogs.OpenFileDialog("VRMファイル選択", ".vrm");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        var vrmdata = new VRMData();
        vrmdata.FilePath = path;
        var context = new VRMImporterContext(path);

        var bytes = File.ReadAllBytes(path);

        // GLB形式でJSONを取得しParseします
        context.ParseVrm(bytes);

        // metaを取得
        var meta = context.ReadMeta(true);
        //サムネイル
        if (meta.Thumbnail != null)
        {
            vrmdata.ThumbnailPNGBytes = meta.Thumbnail.EncodeToPNG(); //Or SaveAsPng( memoryStream, texture.Width, texture.Height )
        }
        //Info
        vrmdata.Title = meta.Title;
        vrmdata.Version = meta.Version;
        vrmdata.Author = meta.Author;
        vrmdata.ContactInformation = meta.ContactInformation;
        vrmdata.Reference = meta.Reference;

        // Permission
        vrmdata.AllowedUser = (ControlWindow.AllowedUser)meta.AllowedUser;
        vrmdata.ViolentUssage = (ControlWindow.UssageLicense)meta.ViolentUssage;
        vrmdata.SexualUssage = (ControlWindow.UssageLicense)meta.SexualUssage;
        vrmdata.CommercialUssage = (ControlWindow.UssageLicense)meta.CommercialUssage;
        vrmdata.OtherPermissionUrl = meta.OtherPermissionUrl;

        // Distribution License
        vrmdata.LicenseType = (ControlWindow.LicenseType)meta.LicenseType;
        vrmdata.OtherLicenseUrl = meta.OtherLicenseUrl;
        /*
        // ParseしたJSONをシーンオブジェクトに変換していく
        var now = Time.time;
        var go = await VRMImporter.LoadVrmAsync(context);

        var delta = Time.time - now;
        Debug.LogFormat("LoadVrmAsync {0:0.0} seconds", delta);
        //OnLoaded(go);
        */
        return vrmdata;
    }

    private async void ImportVRM(string path, bool ImportForCalibration)
    {
        CurrentSettings.VRMPath = path;
        var context = new VRMImporterContext(path);

        var bytes = File.ReadAllBytes(path);

        // GLB形式でJSONを取得しParseします
        context.ParseVrm(bytes);

        if (CurrentModel != null)
        {
            CurrentModel.transform.SetParent(null);
            CurrentModel.SetActive(false);
            Destroy(CurrentModel);
            CurrentModel = null;
        }
        // ParseしたJSONをシーンオブジェクトに変換していく
        CurrentModel = await VRMImporter.LoadVrmAsync(context);

        //LipSync
        LipSync.ImportVRMmodel(CurrentModel);
        //まばたき
        blinkController.ImportVRMmodel(CurrentModel);

        CurrentModel.transform.SetParent(transform, false);

        SetVRIK(CurrentModel);
        if (ImportForCalibration == false)
        {
            SetCameraLookTarget();
            //SetTrackersToVRIK();
        }
        else
        {
            var animator = CurrentModel.GetComponent<Animator>();
            if (animator != null)
            {
                if (CalibrationCamera != null)
                {
                    CalibrationCamera.Target = animator.GetBoneTransform(HumanBodyBones.Head);
                    CalibrationCamera.gameObject.SetActive(true);
                }
            }
        }
    }

    #endregion

    #region Calibration

    private void SetVRIK(GameObject model)
    {
        vrik = model.AddComponent<VRIK>();
        vrik.solver.IKPositionWeight = 0f;
        vrik.solver.leftArm.stretchCurve = new AnimationCurve();
        vrik.solver.rightArm.stretchCurve = new AnimationCurve();
        vrik.UpdateSolverExternal();
    }

    Transform bodyTracker = null;
    Transform leftFootTracker = null;
    Transform rightFootTracker = null;
    Transform leftHandTracker = null;
    Transform rightHandTracker = null;

    private void Calibrate()
    {
        var settings = new VRIKCalibrator.Settings { };
        Transform headTracker = handler.HMDObject.transform;// AddCalibrateTransform(handler.HMDObject.transform, TrackerNums.Zero);
        var controllerTransforms = (new Transform[] { handler.LeftControllerObject.transform, handler.RightControllerObject.transform }).Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.transform.position - headTracker.position), transform = d.transform }).OrderBy(d => d.pos.x).Select(d => d.transform);
        leftHandTracker = controllerTransforms.ElementAtOrDefault(0);// AddCalibrateTransform(handler.LeftControllerObject.transform, TrackerNums.Zero);
        rightHandTracker = controllerTransforms.ElementAtOrDefault(1);// AddCalibrateTransform(handler.RightControllerObject.transform, TrackerNums.Zero);
        var trackerTransforms = handler.Trackers.Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.transform.position - headTracker.position), transform = d.transform }).ToList();
        if (handler.Trackers.Count >= 3)
        {
            //トラッカー3つ以上あれば腰も設定
            bodyTracker = trackerTransforms.OrderByDescending(d => d.pos.y).Select(d => d.transform).First();// handler.Trackers[0].transform;// AddCalibrateTransform(handler.Trackers[0].transform, TrackerNums.Zero);
            leftFootTracker = trackerTransforms.OrderBy(d => d.pos.y).Take(2).OrderBy(d => d.pos.x).Select(d => d.transform).First();// handler.Trackers[2].transform;// AddCalibrateTransform(handler.Trackers[2].transform, TrackerNums.Zero);
            rightFootTracker = trackerTransforms.OrderBy(d => d.pos.y).Take(2).OrderByDescending(d => d.pos.x).Select(d => d.transform).First();// handler.Trackers[1].transform;// AddCalibrateTransform(handler.Trackers[1].transform, TrackerNums.Zero);
        }
        else if (handler.Trackers.Count >= 2)
        {
            //トラッカーが2つだけなら両足
            leftFootTracker = trackerTransforms.OrderBy(d => d.pos.y).Take(2).OrderBy(d => d.pos.x).Select(d => d.transform).First();// handler.Trackers[1].transform;// AddCalibrateTransform(handler.Trackers[1].transform, TrackerNums.Zero);
            rightFootTracker = trackerTransforms.OrderBy(d => d.pos.y).Take(2).OrderByDescending(d => d.pos.x).Select(d => d.transform).First();// handler.Trackers[0].transform;// AddCalibrateTransform(handler.Trackers[0].transform, TrackerNums.Zero);
        }
        else if (handler.Trackers.Count >= 1)
        {
            //トラッカーが1つだけなら腰だけ
            bodyTracker = handler.Trackers[0].transform;// AddCalibrateTransform(handler.Trackers[0].transform, TrackerNums.Zero);
        }
        //DoCalibrate(vrik, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        //DoCalibrate2(vrik, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        vrik.solver.IKPositionWeight = 1.0f;
        VRIKCalibrator.Calibrate(vrik, new VRIKCalibrator.Settings() { headOffset = new Vector3(0f, -0.15f, -0.15f), handOffset = new Vector3(0f, -0.03f, -0.07f) }, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        VRIKCalibrator.Calibrate(vrik, new VRIKCalibrator.Settings() { headOffset = new Vector3(0f, -0.15f, -0.15f), handOffset = new Vector3(0f, -0.03f, -0.07f) }, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        VRIKCalibrator.Calibrate(vrik, new VRIKCalibrator.Settings() { headOffset = new Vector3(0f, -0.15f, -0.15f), handOffset = new Vector3(0f, -0.03f, -0.07f) }, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        if (handler.Trackers.Count == 1)
        {
            vrik.solver.plantFeet = true;
            vrik.solver.locomotion.weight = 1.0f;
            var rootController = vrik.references.root.GetComponent<VRIKRootController>();
            if (rootController != null) GameObject.Destroy(rootController);
        }
        CurrentSettings.headTracker = StoreTransform.Create(headTracker);
        CurrentSettings.bodyTracker = StoreTransform.Create(bodyTracker);
        CurrentSettings.leftHandTracker = StoreTransform.Create(leftHandTracker);
        CurrentSettings.rightHandTracker = StoreTransform.Create(rightHandTracker);
        CurrentSettings.leftFootTracker = StoreTransform.Create(leftFootTracker);
        CurrentSettings.rightFootTracker = StoreTransform.Create(rightFootTracker);
    }

    private void EndCalibrate()
    {
        if (CalibrationCamera != null)
        {
            CalibrationCamera.gameObject.SetActive(false);
        }

        SetCameraLookTarget();
        //SetTrackersToVRIK();
    }

    #endregion

    #region LipSync

    private void SetLipSyncEnable(bool enable)
    {
        LipSync.EnableLipSync = enable;
        CurrentSettings.LipSyncEnable = enable;
    }

    private string[] GetLipSyncDevices()
    {
        return LipSync.GetMicrophoneDevices();
    }

    private void SetLipSyncDevice(string device)
    {
        LipSync.SetMicrophoneDevice(device);
        CurrentSettings.LipSyncDevice = device;
    }

    private void SetLipSyncGain(float gain)
    {
        if (gain < 1.0f) gain = 1.0f;
        if (gain > 256.0f) gain = 256.0f;
        LipSync.Gain = gain;
        CurrentSettings.LipSyncGain = gain;
    }

    private void SetLipSyncMaxWeightEnable(bool enable)
    {
        LipSync.MaxWeightEnable = enable;
        CurrentSettings.LipSyncMaxWeightEnable = enable;
    }

    private void SetLipSyncWeightThreashold(float threashold)
    {
        LipSync.WeightThreashold = threashold;
        CurrentSettings.LipSyncWeightThreashold = threashold;
    }

    private void SetLipSyncMaxWeightEmphasis(bool enable)
    {
        LipSync.MaxWeightEmphasis = enable;
        CurrentSettings.LipSyncMaxWeightEmphasis = enable;
    }

    #endregion

    #region Color

    private void ChangeBackgroundColor(float r, float g, float b, bool isCustom)
    {
        BackgroundRenderer.material.color = new Color(r, g, b, 1.0f);
        CurrentSettings.BackgroundColor = BackgroundRenderer.material.color;
        if (isCustom) CurrentSettings.CustomBackgroundColor = BackgroundRenderer.material.color;
        CurrentSettings.IsTransparent = false;
        SetDwmTransparent(false);
    }

    private void SetBackgroundTransparent()
    {
        CurrentSettings.IsTransparent = true;
#if !UNITY_EDITOR   // エディタ上では動きません。
        BackgroundRenderer.material.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        SetDwmTransparent(true);
#endif
    }

    void SetWindowBorder(bool enable)
    {
        CurrentSettings.HideBorder = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
        var hwnd = GetUnityWindowHandle();
        //var hwnd = GetActiveWindow();
        if (enable)
        {
            SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE); //ウインドウ枠の削除
        }
        else
        {
            SetWindowLong(hwnd, GWL_STYLE, defaultWindowStyle);
        }
#endif
    }
    void SetWindowTopMost(bool enable)
    {
        CurrentSettings.IsTopMost = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
        SetUnityWindowTopMost(enable);
#endif
    }

    void SetWindowClickThrough(bool enable)
    {
        CurrentSettings.HideBorder = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
        var hwnd = GetUnityWindowHandle();
        //var hwnd = GetActiveWindow();
        if (enable)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT); //クリックを透過する
        }
        else
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, defaultExWindowStyle);
        }
#endif
    }

    void OnRenderImage(RenderTexture from, RenderTexture to)
    {
        Graphics.Blit(from, to, BackgroundRenderer.material);
    }

    #endregion

    #region CameraControl


    private Vector3 cameraMouseOldPos; // マウスの位置を保存する変数

    public Camera FreeCamera;
    public Camera FrontCamera;
    public Camera BackCamera;

    private void ChangeCamera(CameraTypes type)
    {
        if (type == CameraTypes.Free)
        {
            SetCameraEnable(FreeCamera);
        }
        else if (type == CameraTypes.Front)
        {
            SetCameraEnable(FrontCamera);
        }
        else if (type == CameraTypes.Back)
        {
            SetCameraEnable(BackCamera);
        }
        CurrentSettings.CameraType = type;
    }

    private void SetCameraEnable(Camera camera)
    {
        if (camera != null)
        {
            camera.gameObject.SetActive(true);
            if (currentCamera != null && currentCamera != camera) currentCamera.gameObject.SetActive(false);
            currentCamera = camera;
            currentCameraLookTarget = camera.gameObject.GetComponent<CameraLookTarget>();
        }

    }

    private void SetCameraLookTarget()
    {
        var animator = CurrentModel?.GetComponent<Animator>();
        if (animator != null)
        {
            var spineTransform = animator.GetBoneTransform(HumanBodyBones.Spine);
            var calcPosition = Vector3.Lerp(animator.GetBoneTransform(HumanBodyBones.Head).position, spineTransform.position, 0.5f);
            var gameObject = new GameObject("CameraLook");
            gameObject.transform.position = calcPosition;
            gameObject.transform.rotation = spineTransform.rotation;
            gameObject.transform.parent = CurrentModel.transform;
            var lookTarget = FrontCamera.GetComponent<CameraLookTarget>();
            if (lookTarget != null)
            {
                lookTarget.Target = gameObject.transform;
            }
            lookTarget = BackCamera.GetComponent<CameraLookTarget>();
            if (lookTarget != null)
            {
                lookTarget.Target = gameObject.transform;
            }
        }
    }

    private void SaveLookTarget(Camera camera)
    {
        if (camera == FrontCamera)
        {
            if (CurrentSettings.FrontCameraLookTargetSettings == null)
            {
                CurrentSettings.FrontCameraLookTargetSettings = LookTargetSettings.Create(currentCameraLookTarget);
            }
            else
            {
                CurrentSettings.FrontCameraLookTargetSettings.Set(currentCameraLookTarget);
            }
        }
        else if (camera == BackCamera)
        {
            if (CurrentSettings.BackCameraLookTargetSettings == null)
            {
                CurrentSettings.BackCameraLookTargetSettings = LookTargetSettings.Create(currentCameraLookTarget);
            }
            else
            {
                CurrentSettings.BackCameraLookTargetSettings.Set(currentCameraLookTarget);
            }
        }
    }

    // マウス関係のイベント
    private void CameraMouseEvent()
    {
        float delta = Input.GetAxis("Mouse ScrollWheel");
        if (delta != 0.0f)
        {
            if (currentCameraLookTarget == null) //フリーカメラ
            {
                currentCamera.transform.position += currentCamera.transform.forward * delta;
                if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(currentCamera.transform);
                CurrentSettings.FreeCameraTransform.SetPosition(currentCamera.transform);
            }
            else //固定カメラ
            {
                currentCameraLookTarget.Distance += delta;
                SaveLookTarget(currentCamera);
            }
        }

        var mousePos = Input.mousePosition;

        // 押されたとき
        if (Input.GetMouseButtonDown((int)MouseButtons.Right) || Input.GetMouseButtonDown((int)MouseButtons.Center))
            cameraMouseOldPos = mousePos;

        Vector3 diff = mousePos - cameraMouseOldPos;

        // 差分の長さが極小数より小さかったら、ドラッグしていないと判断する
        if (diff.magnitude >= Vector3.kEpsilon)
        {

            if (Input.GetMouseButton((int)MouseButtons.Center))
            { // 注視点
                if (currentCameraLookTarget == null) //フリーカメラ
                {
                    currentCamera.transform.Translate(-diff * Time.deltaTime * 1.1f);
                    if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(currentCamera.transform);
                    CurrentSettings.FreeCameraTransform.SetPosition(currentCamera.transform);
                }
                else //固定カメラ
                {
                    currentCameraLookTarget.Offset += new Vector3(0, -diff.y, 0) * Time.deltaTime * 1.1f;
                    SaveLookTarget(currentCamera);
                }
            }
            else if (Input.GetMouseButton((int)MouseButtons.Right))
            { // 回転
                currentCamera.transform.RotateAround(currentCamera.transform.position, currentCamera.transform.right, -diff.y * Time.deltaTime * 30.0f);
                currentCamera.transform.RotateAround(currentCamera.transform.position, Vector3.up, diff.x * Time.deltaTime * 30.0f);
                if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(currentCamera.transform);
                CurrentSettings.FreeCameraTransform.SetRotation(currentCamera.transform);
            }

            this.cameraMouseOldPos = mousePos;
        }
        return;
    }

    private void SetGridVisible(bool enable)
    {
        GridCanvas?.SetActive(enable);
        CurrentSettings.ShowCameraGrid = enable;
    }

    #endregion

    #region BlinkControl
    void SetAutoBlinkEnable(bool enable)
    {
        blinkController.EnableBlink = enable;
        CurrentSettings.AutoBlinkEnable = enable;
    }
    void SetBlinkTimeMin(float time)
    {
        blinkController.BlinkTimeMin = time;
        CurrentSettings.BlinkTimeMin = time;
    }
    void SetBlinkTimeMax(float time)
    {
        blinkController.BlinkTimeMax = time;
        CurrentSettings.BlinkTimeMax = time;
    }
    void SetCloseAnimationTime(float time)
    {
        blinkController.CloseAnimationTime = time;
        CurrentSettings.CloseAnimationTime = time;
    }
    void SetOpenAnimationTime(float time)
    {
        blinkController.OpenAnimationTime = time;
        CurrentSettings.OpenAnimationTime = time;
    }
    void SetClosingTime(float time)
    {
        blinkController.ClosingTime = time;
        CurrentSettings.ClosingTime = time;
    }

    private Dictionary<string, BlendShapePreset> BlendShapeNameDictionary = new Dictionary<string, BlendShapePreset>
    {
        { "通常(NEUTRAL)", BlendShapePreset.Neutral },
        { "喜(JOY)", BlendShapePreset.Joy },
        { "怒(ANGRY)", BlendShapePreset.Angry },
        { "哀(SORROW)", BlendShapePreset.Sorrow },
        { "楽(FUN)", BlendShapePreset.Fun },
        { "上見(LOOKUP)", BlendShapePreset.LookUp },
        { "下見(LOOKDOWN)", BlendShapePreset.LookDown },
        { "左見(LOOKLEFT)", BlendShapePreset.LookLeft },
        { "右見(LOOKRIGHT)", BlendShapePreset.LookRight },
    };

    void SetDefaultFace(string face)
    {
        if (string.IsNullOrEmpty(face))
        {
            blinkController.DefaultFace = BlendShapePreset.Neutral;
            blinkController.FacePresetName = null;
        }
        else if (BlendShapeNameDictionary.ContainsKey(face))
        {
            blinkController.DefaultFace = BlendShapeNameDictionary[face];
            blinkController.FacePresetName = null;
        }
        else
        {
            blinkController.DefaultFace = BlendShapePreset.Unknown;
            blinkController.FacePresetName = face;
        }
    }
    #endregion

    #region Setting

    [Serializable]
    private class StoreTransform
    {
        public Vector3 localPosition;
        public Vector3 position;
        public Quaternion localRotation;
        public Quaternion rotation;
        public Vector3 localScale;

        public StoreTransform() { }
        public StoreTransform(Transform orig) : this()
        {
            localPosition = orig.localPosition;
            position = orig.position;
            localRotation = orig.localRotation;
            rotation = orig.rotation;
            localScale = orig.localScale;
        }

        public static StoreTransform Create(Transform orig)
        {
            if (orig == null) return null;
            return new StoreTransform(orig);
        }

        public void SetPosition(Transform orig)
        {
            localPosition = orig.position;
            position = orig.position;
        }

        public void SetRotation(Transform orig)
        {
            localRotation = orig.localRotation;
            rotation = orig.rotation;
        }

        public Transform ToLocalTransform(Transform saveto)
        {
            saveto.localPosition = localPosition;
            saveto.localRotation = localRotation;
            saveto.localScale = localScale;
            return saveto;
        }

        public Transform ToWorldTransform(Transform saveto)
        {
            saveto.position = position;
            saveto.rotation = rotation;
            saveto.localScale = localScale;
            return saveto;
        }
    }

    [Serializable]
    private class LookTargetSettings
    {
        public Vector3 Offset;
        public float Distance;
        public static LookTargetSettings Create(CameraLookTarget target)
        {
            return new LookTargetSettings { Offset = target.Offset, Distance = target.Distance };
        }
        public void Set(CameraLookTarget target)
        {
            Offset = target.Offset; Distance = target.Distance;
        }
        public void ApplyTo(CameraLookTarget target)
        {
            target.Offset = Offset; target.Distance = Distance;
        }
        public void ApplyTo(Camera camera)
        {
            var target = camera.GetComponent<CameraLookTarget>();
            if (target != null) { target.Offset = Offset; target.Distance = Distance; }
        }
    }

    [Serializable]
    private class Settings
    {
        public string VRMPath = null;
        public StoreTransform headTracker = null;
        public StoreTransform bodyTracker = null;
        public StoreTransform leftHandTracker = null;
        public StoreTransform rightHandTracker = null;
        public StoreTransform leftFootTracker = null;
        public StoreTransform rightFootTracker = null;
        public Color BackgroundColor;
        public Color CustomBackgroundColor;
        public bool IsTransparent;
        public bool HideBorder;
        public bool IsTopMost;
        public StoreTransform FreeCameraTransform = null;
        public LookTargetSettings FrontCameraLookTargetSettings = null;
        public LookTargetSettings BackCameraLookTargetSettings = null;
        [OptionalField]
        public CameraTypes? CameraType = null;
        [OptionalField]
        public bool ShowCameraGrid = false;
        [OptionalField]
        public bool WindowClickThrough;
        [OptionalField]
        public bool LipSyncEnable;
        [OptionalField]
        public string LipSyncDevice;
        [OptionalField]
        public float LipSyncGain;
        [OptionalField]
        public bool LipSyncMaxWeightEnable;
        [OptionalField]
        public float LipSyncWeightThreashold;
        [OptionalField]
        public bool LipSyncMaxWeightEmphasis;
        [OptionalField]
        public bool AutoBlinkEnable = false;
        [OptionalField]
        public float BlinkTimeMin = 1.0f;
        [OptionalField]
        public float BlinkTimeMax = 10.0f;
        [OptionalField]
        public float CloseAnimationTime = 0.06f;
        [OptionalField]
        public float OpenAnimationTime = 0.03f;
        [OptionalField]
        public float ClosingTime = 0.1f;
        [OptionalField]
        public string DefaultFace = "通常(NEUTRAL)";

        //初期値
        [OnDeserializing()]
        internal void OnDeserializingMethod(StreamingContext context)
        {
            BlinkTimeMin = 1.0f;
            BlinkTimeMax = 10.0f;
            CloseAnimationTime = 0.06f;
            OpenAnimationTime = 0.03f;
            ClosingTime = 0.1f;
            DefaultFace = "通常(NEUTRAL)";
        }
    }

    private Settings CurrentSettings = new Settings();

    private void SaveSettings()
    {
        var path = WindowsDialogs.SaveFileDialog("設定保存先選択", ".json");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        File.WriteAllText(path, Json.Serializer.Serialize(CurrentSettings));
    }

    private void LoadSettings()
    {
        var path = WindowsDialogs.OpenFileDialog("設定読み込み先選択", ".json");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        CurrentSettings = Json.Serializer.Deserialize<Settings>(File.ReadAllText(path));
        if (CurrentSettings != null)
        {
            if (string.IsNullOrWhiteSpace(CurrentSettings.VRMPath) == false)
            {
                win.CurrentVRMFilePath = CurrentSettings.VRMPath;
                ImportVRM(CurrentSettings.VRMPath, false);
            }
            if (CurrentSettings.BackgroundColor != null)
            {
                ChangeBackgroundColor(CurrentSettings.BackgroundColor.r, CurrentSettings.BackgroundColor.g, CurrentSettings.BackgroundColor.b, false);
            }
            if (CurrentSettings.CustomBackgroundColor != null)
            {
                WindowLoader.Instance.LoadCustomBackgroundColor?.Invoke(CurrentSettings.CustomBackgroundColor.r, CurrentSettings.CustomBackgroundColor.g, CurrentSettings.CustomBackgroundColor.b);
            }
            if (CurrentSettings.IsTransparent)
            {
                SetBackgroundTransparent();
            }
            SetWindowBorder(CurrentSettings.HideBorder);
            WindowLoader.Instance.LoadHideBorder?.Invoke(CurrentSettings.HideBorder);
            SetWindowTopMost(CurrentSettings.IsTopMost);
            WindowLoader.Instance.LoadIsTopMost?.Invoke(CurrentSettings.IsTopMost);
            if (CurrentSettings.FreeCameraTransform != null)
            {
                CurrentSettings.FreeCameraTransform.ToLocalTransform(FreeCamera.transform);
            }
            if (CurrentSettings.FrontCameraLookTargetSettings != null)
            {
                CurrentSettings.FrontCameraLookTargetSettings.ApplyTo(FrontCamera);
            }
            if (CurrentSettings.BackCameraLookTargetSettings != null)
            {
                CurrentSettings.BackCameraLookTargetSettings.ApplyTo(BackCamera);
            }
            if (CurrentSettings.CameraType.HasValue)
            {
                ChangeCamera(CurrentSettings.CameraType.Value);
            }
            SetGridVisible(CurrentSettings.ShowCameraGrid);
            WindowLoader.Instance.LoadShowCameraGrid?.Invoke(CurrentSettings.ShowCameraGrid);
            SetWindowClickThrough(CurrentSettings.WindowClickThrough);
            WindowLoader.Instance.LoadSetWindowClickThrough?.Invoke(CurrentSettings.WindowClickThrough);
            SetLipSyncEnable(CurrentSettings.LipSyncEnable);
            WindowLoader.Instance.LoadLipSyncEnable?.Invoke(CurrentSettings.LipSyncEnable);
            SetLipSyncDevice(CurrentSettings.LipSyncDevice);
            WindowLoader.Instance.LoadLipSyncDevice?.Invoke(CurrentSettings.LipSyncDevice);
            SetLipSyncGain(CurrentSettings.LipSyncGain);
            WindowLoader.Instance.LoadLipSyncGain(CurrentSettings.LipSyncGain);
            SetLipSyncMaxWeightEnable(CurrentSettings.LipSyncMaxWeightEnable);
            WindowLoader.Instance.LoadLipSyncMaxWeightEnable?.Invoke(CurrentSettings.LipSyncMaxWeightEnable);
            SetLipSyncWeightThreashold(CurrentSettings.LipSyncWeightThreashold);
            WindowLoader.Instance.LoadLipSyncWeightThreashold?.Invoke(CurrentSettings.LipSyncWeightThreashold);
            SetLipSyncMaxWeightEmphasis(CurrentSettings.LipSyncMaxWeightEmphasis);
            WindowLoader.Instance.LoadLipSyncMaxWeightEmphasis?.Invoke(CurrentSettings.LipSyncMaxWeightEmphasis);

            SetAutoBlinkEnable(CurrentSettings.AutoBlinkEnable);
            WindowLoader.Instance.LoadAutoBlinkEnable?.Invoke(CurrentSettings.AutoBlinkEnable);
            SetBlinkTimeMin(CurrentSettings.BlinkTimeMin);
            WindowLoader.Instance.LoadBlinkTimeMin?.Invoke(CurrentSettings.BlinkTimeMin);
            SetBlinkTimeMax(CurrentSettings.BlinkTimeMax);
            WindowLoader.Instance.LoadBlinkTimeMax?.Invoke(CurrentSettings.BlinkTimeMax);
            SetCloseAnimationTime(CurrentSettings.CloseAnimationTime);
            WindowLoader.Instance.LoadCloseAnimationTime?.Invoke(CurrentSettings.CloseAnimationTime);
            SetOpenAnimationTime(CurrentSettings.OpenAnimationTime);
            WindowLoader.Instance.LoadOpenAnimationTime?.Invoke(CurrentSettings.OpenAnimationTime);
            SetClosingTime(CurrentSettings.ClosingTime);
            WindowLoader.Instance.LoadClosingTime?.Invoke(CurrentSettings.ClosingTime);
            SetDefaultFace(CurrentSettings.DefaultFace);
            WindowLoader.Instance.LoadDefaultFace?.Invoke(CurrentSettings.DefaultFace);
        }
    }

    #endregion

    private void Awake()
    {
        defaultWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_STYLE);
        defaultExWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_EXSTYLE);
    }

    private void TestEvent(object temp)
    {
        var vrik = CurrentModel.GetComponent<VRIK>();
        if (vrik == null) return;
        vrik.fixTransforms = false;
        vrik.fixTransforms = true;
    }


    private class TimerAction
    {
        public float second;
        public Action action;
    }
    ConcurrentDictionary<TimerAction, int> TimerDictionary = new ConcurrentDictionary<TimerAction, int>();

    private void RunAfterMs(int ms, Action action)
    {
        TimerDictionary.TryAdd(new TimerAction { second = (float)ms / 1000f, action = action }, 0);
    }

    ConcurrentQueue<Action> ActionQueue = new ConcurrentQueue<Action>();
    private void RunOnUnity(Action action)
    {
        ActionQueue.Enqueue(action);
    }

    // Update is called once per frame
    void Update()
    {
        Action action;
        while (ActionQueue.TryDequeue(out action))
        {
            action();
        }

        foreach (var pair in TimerDictionary)
        {
            pair.Key.second -= Time.deltaTime;
            if (pair.Key.second <= 0f)
            {
                pair.Key.action();
                int _;
                TimerDictionary.TryRemove(pair.Key, out _);
            }
        }

        CameraMouseEvent();
    }

    private int WindowX;
    private int WindowY;
    private Vector2 OldMousePos;

    void LateUpdate()
    {
        //Windowの移動操作
        //ドラッグ開始
        if (Input.GetMouseButtonDown(0))
        {
            var r = GetUnityWindowPosition();
            WindowX = r.left;
            WindowY = r.top;
            OldMousePos = GetWindowsMousePosition();
        }

        //ドラッグ中
        if (Input.GetMouseButton(0))
        {
            Vector2 pos = GetWindowsMousePosition();
            if (pos != OldMousePos)
            {
                WindowX += (int)(pos.x - OldMousePos.x);
                WindowY += (int)(pos.y - OldMousePos.y);
                SetUnityWindowPosition(WindowX, WindowY);
                OldMousePos = pos;
            }
        }
    }
}
