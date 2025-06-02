namespace KoreanToJapaneseTTS
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            statusLabel = new System.Windows.Forms.Label();
            startButton = new System.Windows.Forms.Button();
            inputTextBox = new System.Windows.Forms.TextBox();
            clientIdTextBox = new System.Windows.Forms.TextBox();
            clientSecretTextBox = new System.Windows.Forms.TextBox();
            speechRateTrackBar = new System.Windows.Forms.TrackBar();
            labelApiKey = new System.Windows.Forms.Label();
            labelRegion = new System.Windows.Forms.Label();
            labelSpeechRate = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)speechRateTrackBar).BeginInit();
            SuspendLayout();
            // 
            // statusLabel
            // 
            statusLabel.AutoSize = true;
            statusLabel.Location = new System.Drawing.Point(20, 17);
            statusLabel.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new System.Drawing.Size(0, 25);
            statusLabel.TabIndex = 0;
            // 
            // startButton
            // 
            startButton.Location = new System.Drawing.Point(20, 444);
            startButton.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            startButton.Name = "startButton";
            startButton.Size = new System.Drawing.Size(125, 44);
            startButton.TabIndex = 1;
            startButton.Text = "시작";
            startButton.UseVisualStyleBackColor = true;
            startButton.Click += StartButton_Click;
            // 
            // inputTextBox
            // 
            inputTextBox.Location = new System.Drawing.Point(20, 62);
            inputTextBox.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            inputTextBox.Multiline = true;
            inputTextBox.Name = "inputTextBox";
            inputTextBox.ReadOnly = true;
            inputTextBox.Size = new System.Drawing.Size(431, 112);
            inputTextBox.TabIndex = 2;
            // 
            // clientIdTextBox
            // 
            clientIdTextBox.Location = new System.Drawing.Point(155, 188);
            clientIdTextBox.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            clientIdTextBox.Name = "clientIdTextBox";
            clientIdTextBox.PasswordChar = '*';
            clientIdTextBox.Size = new System.Drawing.Size(296, 31);
            clientIdTextBox.TabIndex = 3;
            // 
            // clientSecretTextBox
            // 
            clientSecretTextBox.Location = new System.Drawing.Point(155, 238);
            clientSecretTextBox.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            clientSecretTextBox.Name = "clientSecretTextBox";
            clientSecretTextBox.Size = new System.Drawing.Size(296, 31);
            clientSecretTextBox.TabIndex = 4;
            // 
            // speechRateTrackBar
            // 
            speechRateTrackBar.Location = new System.Drawing.Point(155, 288);
            speechRateTrackBar.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            speechRateTrackBar.Minimum = -10;
            speechRateTrackBar.Name = "speechRateTrackBar";
            speechRateTrackBar.Size = new System.Drawing.Size(298, 69);
            speechRateTrackBar.TabIndex = 5;
            speechRateTrackBar.ValueChanged += SpeechRateTrackBar_ValueChanged;
            // 
            // labelApiKey
            // 
            labelApiKey.AutoSize = true;
            labelApiKey.Location = new System.Drawing.Point(20, 194);
            labelApiKey.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            labelApiKey.Name = "labelApiKey";
            labelApiKey.Size = new System.Drawing.Size(119, 25);
            labelApiKey.TabIndex = 7;
            labelApiKey.Text = "Azure API 키:";
            // 
            // labelRegion
            // 
            labelRegion.AutoSize = true;
            labelRegion.Location = new System.Drawing.Point(20, 244);
            labelRegion.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            labelRegion.Name = "labelRegion";
            labelRegion.Size = new System.Drawing.Size(104, 25);
            labelRegion.TabIndex = 8;
            labelRegion.Text = "Azure 리전:";
            // 
            // labelSpeechRate
            // 
            labelSpeechRate.AutoSize = true;
            labelSpeechRate.Location = new System.Drawing.Point(20, 288);
            labelSpeechRate.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            labelSpeechRate.Name = "labelSpeechRate";
            labelSpeechRate.Size = new System.Drawing.Size(94, 25);
            labelSpeechRate.TabIndex = 9;
            labelSpeechRate.Text = "음성 속도:";
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(473, 502);
            Controls.Add(labelSpeechRate);
            Controls.Add(labelRegion);
            Controls.Add(labelApiKey);
            Controls.Add(speechRateTrackBar);
            Controls.Add(clientSecretTextBox);
            Controls.Add(clientIdTextBox);
            Controls.Add(inputTextBox);
            Controls.Add(startButton);
            Controls.Add(statusLabel);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            MaximizeBox = false;
            Name = "Form1";
            Text = "Korean to Japanese TTS";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)speechRateTrackBar).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.TextBox inputTextBox;
        private System.Windows.Forms.TextBox clientIdTextBox;
        private System.Windows.Forms.TextBox clientSecretTextBox;
        private System.Windows.Forms.TrackBar speechRateTrackBar;
        private System.Windows.Forms.Label labelApiKey;
        private System.Windows.Forms.Label labelRegion;
        private System.Windows.Forms.Label labelSpeechRate;
    }
}