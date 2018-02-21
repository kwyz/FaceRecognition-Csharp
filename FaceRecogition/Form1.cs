using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Luxand;
using System.IO;
using System.Diagnostics;

namespace LiveRecognition
{
    public partial class Form1 : Form
    {
        String cameraName;
        bool needClose = false;
        enum ProgramState { psRemember, psRecognize }
        ProgramState programState = ProgramState.psRecognize;
        string userName;
        String TrackerMemoryFile = "tracker.dat";

        int mouseX = 0;
        int mouseY = 0;

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (FSDK.FSDKE_OK != FSDK.ActivateLibrary("K1ueYIEDPy8ua3P21gjImB7sLfGWbI3UZZssU8P3gIocnsEPsKsyMj6HsPFFcYHUVG9FcSV6kYwnV4JwwW5mt78FUvpEumSAFKpqNEyw6XOr0OyOgwYf3E/64wawVk5i5ULX5kAk12j4/ZNqKi2RtQ9HrzEV/BSgYHGx3ovUtHk="))
            {
                MessageBox.Show("Please run the License Key Wizard (Start - Luxand - FaceSDK - License Key Wizard)", "Error activating FaceSDK", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            FSDK.InitializeLibrary();
            FSDKCam.InitializeCapturing();

            string[] cameraList;
            int count;
            FSDKCam.GetCameraList(out cameraList, out count);

            if (0 == count)
            {
                MessageBox.Show("Please attach a camera", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
            comboBox1.Items.AddRange(cameraList);
            cameraName = cameraList[0];



            FSDKCam.VideoFormatInfo[] formatList;
            FSDKCam.GetVideoFormatList(ref cameraName, out formatList, out count);

            int VideoFormat = 0;
            pictureBox1.Width = formatList[VideoFormat].Width;
            pictureBox1.Height = formatList[VideoFormat].Height;
            this.Width = formatList[VideoFormat].Width + 48;
            this.Height = formatList[VideoFormat].Height + 96;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            needClose = true;
            Process.GetCurrentProcess().Kill();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.button1.Enabled = false;
            button2.Enabled = true;
            int cameraHandle = 0;
            needClose = false;

            int r = FSDKCam.OpenVideoCamera(ref cameraName, ref cameraHandle);
            if (r != FSDK.FSDKE_OK)
            {
                MessageBox.Show("Error opening the first camera", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            int tracker = 0;
            if (FSDK.FSDKE_OK != FSDK.LoadTrackerMemoryFromFile(ref tracker, TrackerMemoryFile)) // try to load saved tracker state
                FSDK.CreateTracker(ref tracker);

            int err = 0; // set realtime face detection parameters
            FSDK.SetTrackerMultipleParameters(tracker, "RecognizeFaces = true; DetectExpression = true; DetectAge=true; DetectGender=true; HandleArbitraryRotations=true; DetermineFaceRotationAngle=true; InternalResizeWidth=100; FaceDetectionThreshold=5;", ref err);

            while (!needClose)
            {
                Int32 imageHandle = 0;
                if (FSDK.FSDKE_OK != FSDKCam.GrabFrame(cameraHandle, ref imageHandle)) // grab the current frame from the camera
                {
                    Application.DoEvents();
                    continue;
                }
                FSDK.CImage image = new FSDK.CImage(imageHandle);

                long[] IDs;
                long faceCount = 0;
                FSDK.FeedFrame(tracker, 0, image.ImageHandle, ref faceCount, out IDs, sizeof(long) * 256); // maximum of 256 faces detected
                Array.Resize(ref IDs, (int)faceCount);

                Image frameImage = image.ToCLRImage();
                if (checkBox1.Checked)
                {
                    pictureBox1.Image = drawFacialFlandmarks(IDs, tracker, frameImage);
                }
                else
                {
                    pictureBox1.Image = frameImage;
                }
                GC.Collect();
                Application.DoEvents();
            }

            FSDK.SaveTrackerMemoryToFile(tracker, TrackerMemoryFile);
            FSDK.FreeTracker(tracker);
            FSDKCam.CloseVideoCamera(cameraHandle);
            FSDKCam.FinalizeCapturing();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            needClose = true;
            pictureBox1.Image = null;
            button1.Enabled = true;
            button2.Enabled = false;

        }
        public void GuesGenderAndAge(long[] IDs, int i, int tracker)
        {
            String AttributeValues;
            String AttributeValuesAge;
            String AtributValuesExpression;
            string str = "";
            if (0 == FSDK.GetTrackerFacialAttribute(tracker, 0, IDs[i], "Gender", out AttributeValues, 1024)
                && 0 == FSDK.GetTrackerFacialAttribute(tracker, 0, IDs[i], "Age", out AttributeValuesAge, 1024)
                && 0 == FSDK.GetTrackerFacialAttribute(tracker, 0, IDs[i], "Expression", out AtributValuesExpression, 1024))
            {
                if (checkBox2.Checked)
                {
                    float ConfidenceAge = 0.0f;
                    FSDK.GetValueConfidence(AttributeValuesAge, "Age", ref ConfidenceAge);
                    label7.Text = ((int)ConfidenceAge).ToString();
                }
                else
                {
                    label7.Text = "";
                }
                if (checkBox3.Checked)
                {
                    float ConfidenceMale = 0.0f;
                    float ConfidenceFemale = 0.0f;
                    FSDK.GetValueConfidence(AttributeValues, "Male", ref ConfidenceMale);
                    FSDK.GetValueConfidence(AttributeValues, "Female", ref ConfidenceFemale);
                    label8.Text = (ConfidenceMale > ConfidenceFemale ? "Barbat" : "Femeie") + " " + (ConfidenceMale > ConfidenceFemale ? (int)(ConfidenceMale * 100) : (int)(ConfidenceFemale * 100)).ToString() + "%";
                }
                else
                {
                    label8.Text = "";
                }
                if (checkBox4.Checked)
                {
                    float ConfidenceExpression = 0.0f;
                    FSDK.GetValueConfidence(AtributValuesExpression, "Expression", ref ConfidenceExpression);
                    string[] tokens = AtributValuesExpression.Split(';');
                    label9.Text = tokens[0];

                }
                else
                {
                    label9.Text = "";
                }


            }
        }

        public Image drawFacialFlandmarks(long[] IDs, int tracker, Image frameImage)
        {
            Graphics gr = Graphics.FromImage(frameImage);
            Pen pen = new Pen(Brushes.Red);
            pen.Width = 5.0F;
            pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Bevel;
            for (int i = 0; i < IDs.Length; ++i)
            {
                FSDK.TFacePosition facePosition = new FSDK.TFacePosition();
                FSDK.GetTrackerFacePosition(tracker, 0, IDs[i], ref facePosition);
                FSDK.TPoint[] facialFeatures;
                FSDK.GetTrackerFacialFeatures(tracker, 0, IDs[i], out facialFeatures);
                int left = facePosition.xc - (int)(facePosition.w * 0.6);
                int top = facePosition.yc - (int)(facePosition.w * 0.5);
                StringFormat format = new StringFormat();

                for (int x = 0; x < facialFeatures.Length; x++)
                {
                    FSDK.TPoint point = facialFeatures[x];
                    gr.FillEllipse(Brushes.DarkBlue, point.x, point.y, 4, 4);

                    GuesGenderAndAge(IDs, i, tracker);
                    personRecognize(IDs, facePosition, left, top, facePosition.w, i, tracker, frameImage);
                }
            }
            return frameImage;
        }


        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            cameraName = comboBox1.SelectedItem.ToString();
        }


        public void personRecognize(long[] IDs, FSDK.TFacePosition facePosition, int left, int top, int w, int i, int tracker, Image frameImage)
        {

            String name;
            Graphics gr = Graphics.FromImage(frameImage);
            int res = FSDK.GetAllNames(tracker, IDs[i], out name, 65536); // maximum of 65536 characters
            if (FSDK.FSDKE_OK == res && name.Length > 0)
            { // draw name
                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Center;

                gr.DrawString(name, new Font("Arial", 16),
                    new SolidBrush(Color.LightGreen),
                    facePosition.xc, top + w + 5, format);
            }
            Pen pen = Pens.LightGreen;
            if (mouseX >= left && mouseX <= left + w && mouseY >= top && mouseY <= top + w)
            {
                pen = Pens.Blue;

                if (ProgramState.psRemember == programState)
                {
                    if (FSDK.FSDKE_OK == FSDK.LockID(tracker, IDs[i]))
                    {
                        Console.WriteLine(mouseY + "Mouse Move2" + mouseX);
                        // get the user name
                        InputName inputName = new InputName();
                        if (DialogResult.OK == inputName.ShowDialog())
                        {
                            Console.WriteLine(mouseY + "Mouse Move3" + mouseX);
                            userName = inputName.userName;
                            if (userName == null || userName.Length <= 0)
                            {
                                String s = "";
                                FSDK.SetName(tracker, IDs[i], "");
                                FSDK.PurgeID(tracker, IDs[i]);
                            }
                            else
                            {
                                FSDK.SetName(tracker, IDs[i], userName);
                            }
                            FSDK.UnlockID(tracker, IDs[i]);
                        }
                    }
                }
                gr.DrawRectangle(pen, left, top, w, w);

            }
            programState = ProgramState.psRecognize;
            pictureBox1.Image = frameImage;

        }
        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            programState = ProgramState.psRemember;
        }
        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            mouseX = e.X;
            mouseY = e.Y;
        }

        private void pictureBox1_MouseLeave(object sender, EventArgs e)
        {
            mouseX = 0;
            mouseY = 0;
        }
    }
}
