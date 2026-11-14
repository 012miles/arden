using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rocket
{
    /// <summary>
    /// Milestone 27 (captaincarl24's pick): photo mode. F2 (or Photo mode in the pause menu) freezes the flight and hides the
    /// HUD; the camera flies free round the ship (WASD, Q/E down and up, SHIFT faster, right-drag to turn, scroll to go
    /// closer or further); a small panel sets a filter, the time of day (the light only), the zoom and the brightness;
    /// SPACE saves a picture to the Photos folder.
    /// </summary>
    public partial class FlightMode
    {
        public bool photoMode;
        float photoFov = 60f, photoShift, photoBright;
        int photoFilter, photoHide;
        bool photoPanelHidden;
        Vector3 photoOffset;
        string photoNote;
        float photoNoteUntil;
        public string lastPhotoPath;

        public static readonly string[] PhotoFilters = { "Normal", "Vivid", "Warm", "Cool", "Black and white", "Old photo", "Dramatic", "Dreamy" };

        public static string PhotoFolder => Path.Combine(Application.persistentDataPath, "Photos");

        public void TogglePhoto()
        {
            photoMode = !photoMode;
            if (photoMode)
            {
                photoOffset = Vector3.zero;
                photoPanelHidden = false;
                SetWarpOne();
                RenderEnv.SetPhotoLook(photoFilter, photoBright);
            }
            else
            {
                RenderEnv.SetPhotoLook(0, 0f, false);
                root.farCam.fieldOfView = 60f;
                root.nearCam.fieldOfView = 60f;
            }
        }

        /// <summary>For the screenshot tour: set the look directly.</summary>
        public void SetPhoto(int filter, float shiftHours, float fov, float bright)
        {
            photoFilter = filter; photoShift = shiftHours; photoFov = fov; photoBright = bright;
            RenderEnv.SetPhotoLook(photoFilter, photoBright);
        }

        /// <summary>Photo mode's keys (the flight is frozen): fly the camera, turn it, take a picture, leave.</summary>
        void HandlePhoto()
        {
            if (In.Down(K.Escape)) { TogglePhoto(); return; }
            if (In.Down(K.Space)) TakePhoto();
            if (In.Down(K.H)) photoPanelHidden = !photoPanelHidden;
            Vector3 up = renderRel.normalized.ToFloat();
            Vector3 camFwd = root.farCam.transform.forward;
            Vector3 fwd = Vector3.ProjectOnPlane(camFwd, up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(root.farCam.transform.up, up);
            fwd.Normalize();
            Vector3 right = Vector3.Cross(up, fwd);
            float speed = Mathf.Clamp(camDist * 0.9f, 3f, 250f) * (In.Held(K.LeftShift) ? 5f : 1f);
            Vector3 move = fwd * ((In.Held(K.W) ? 1 : 0) - (In.Held(K.S) ? 1 : 0)) + right * ((In.Held(K.D) ? 1 : 0) - (In.Held(K.A) ? 1 : 0))
                           + up * ((In.Held(K.E) ? 1 : 0) - (In.Held(K.Q) ? 1 : 0));
            photoOffset = Vector3.ClampMagnitude(photoOffset + move * speed * Mathf.Min(Time.unscaledDeltaTime, 0.1f), 3000f);
            active.inputPitch = active.inputYaw = active.inputRoll = active.driveInput = 0;
            bool overUi = Ui.MouseOverUI(In.MousePosition);
            if (In.RightMouse)
            {
                var d = In.MouseDelta;
                camYaw += d.x * 0.25f;
                camPitch = Mathf.Clamp(camPitch - d.y * 0.25f, -85f, 85f);
            }
            float sc = In.Scroll;
            if (Mathf.Abs(sc) > 0.01f && !overUi) camDist = Mathf.Clamp(camDist * (sc > 0 ? 1f / 1.15f : 1.15f), 1.5f, 5000f);
        }

        void TakePhoto()
        {
            if (photoHide == 0) photoHide = 2;
        }

        /// <summary>Called first thing each frame: a picture asked for last frame is taken now, with nothing drawn over it.</summary>
        void UpdatePhotoCapture()
        {
            if (photoHide == 2)
            {
                try
                {
                    Directory.CreateDirectory(PhotoFolder);
                    lastPhotoPath = Path.Combine(PhotoFolder, $"Arden {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png");
                    ScreenCapture.CaptureScreenshot(lastPhotoPath);
                    photoNote = $"Saved: {Path.GetFileName(lastPhotoPath)}";
                    Achievements.Award(U.career, "photo");
                }
                catch (Exception e) { photoNote = "Couldn't save the picture: " + e.Message; }
                photoHide = 1;
            }
            else if (photoHide == 1) { photoHide = 0; photoNoteUntil = Time.unscaledTime + 4f; }
        }

        /// <summary>The sun, turned round the world's axis by the photo's time of day (the light only; the flight's clock doesn't move).</summary>
        Vector3 PhotoSun(Vector3 sun)
        {
            if (!photoMode || Mathf.Abs(photoShift) < 0.01f) return sun;
            Vector3 axis = active.body.RotationAt(renderUt) * Vector3.up;
            float deg = photoShift / (float)(active.body.rotationPeriod / 3600.0) * 360f;
            return Quaternion.AngleAxis(-deg, axis) * sun;
        }

        void DrawPhoto(float W, float H)
        {
            if (photoHide > 0) return;
            if (photoNote != null && Time.unscaledTime < photoNoteUntil)
            {
                var nr = new Rect(W * 0.5f - 220, 20, 440, 30);
                GUI.Box(nr, GUIContent.none, Ui.Panel);
                GUI.Label(nr, photoNote, Ui.Center);
            }
            if (photoPanelHidden)
            {
                GUI.Label(new Rect(16, H - 30, 600, 22), "Photo mode: H shows the panel, SPACE takes a picture, F2 goes back", Ui.Small);
                return;
            }
            var r = new Rect(W * 0.5f - 470, H - 112, 940, 100);
            GUI.Box(r, GUIContent.none, Ui.Panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 150, 26), "PHOTO MODE", Ui.Header);
            float x = r.x + 170, y = r.y + 10;
            bool changed = false;
            // filter
            GUI.Label(new Rect(x, y + 4, 44, 22), "Look", Ui.Small);
            if (GUI.Button(new Rect(x + 44, y, 26, 26), "<", Ui.ButtonSmall)) { photoFilter = (photoFilter + PhotoFilters.Length - 1) % PhotoFilters.Length; changed = true; }
            GUI.Label(new Rect(x + 72, y + 3, 118, 22), PhotoFilters[photoFilter], Ui.Center);
            if (GUI.Button(new Rect(x + 192, y, 26, 26), ">", Ui.ButtonSmall)) { photoFilter = (photoFilter + 1) % PhotoFilters.Length; changed = true; }
            x += 234;
            // time of day
            GUI.Label(new Rect(x, y + 4, 44, 22), "Time", Ui.Small);
            if (GUI.Button(new Rect(x + 44, y, 26, 26), "-", Ui.ButtonSmall)) photoShift = Mathf.Max(-12f, photoShift - 0.5f);
            GUI.Label(new Rect(x + 72, y + 3, 74, 22), Mathf.Abs(photoShift) < 0.01f ? "now" : $"{(photoShift > 0 ? "+" : "")}{photoShift:0.#} h", Ui.Center);
            if (GUI.Button(new Rect(x + 148, y, 26, 26), "+", Ui.ButtonSmall)) photoShift = Mathf.Min(12f, photoShift + 0.5f);
            x += 190;
            // zoom
            GUI.Label(new Rect(x, y + 4, 44, 22), "Zoom", Ui.Small);
            if (GUI.Button(new Rect(x + 44, y, 26, 26), "-", Ui.ButtonSmall)) photoFov = Mathf.Min(100f, photoFov + 5f);
            GUI.Label(new Rect(x + 72, y + 3, 54, 22), $"{60f / photoFov:0.0}x", Ui.Center);
            if (GUI.Button(new Rect(x + 128, y, 26, 26), "+", Ui.ButtonSmall)) photoFov = Mathf.Max(10f, photoFov - 5f);
            x += 170;
            // brightness
            GUI.Label(new Rect(x, y + 4, 60, 22), "Bright", Ui.Small);
            if (GUI.Button(new Rect(x + 54, y, 26, 26), "-", Ui.ButtonSmall)) { photoBright = Mathf.Max(-2f, photoBright - 0.25f); changed = true; }
            GUI.Label(new Rect(x + 82, y + 3, 44, 22), $"{(photoBright > 0 ? "+" : "")}{photoBright:0.##}", Ui.Center);
            if (GUI.Button(new Rect(x + 128, y, 26, 26), "+", Ui.ButtonSmall)) { photoBright = Mathf.Min(2f, photoBright + 0.25f); changed = true; }
            if (changed) RenderEnv.SetPhotoLook(photoFilter, photoBright);

            y = r.y + 54;
            x = r.x + 14;
            if (GUI.Button(new Rect(x, y, 190, 32), "Take picture (SPACE)", Ui.Button)) TakePhoto();
            if (GUI.Button(new Rect(x + 198, y, 150, 32), "Open photos", Ui.Button))
            {
                try { Directory.CreateDirectory(PhotoFolder); Application.OpenURL("file:///" + PhotoFolder.Replace('\\', '/')); } catch { }
            }
            if (GUI.Button(new Rect(x + 356, y, 110, 32), "Hide (H)", Ui.Button)) photoPanelHidden = true;
            if (GUI.Button(new Rect(x + 474, y, 110, 32), "Back (F2)", Ui.Button)) { TogglePhoto(); return; }
            GUI.Label(new Rect(x + 596, y - 2, r.width - 610, 40), "WASD move, Q/E down/up, SHIFT faster\nright-drag to turn, scroll closer/further", Ui.Small);
        }
    }
}
