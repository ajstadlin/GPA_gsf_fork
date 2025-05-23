﻿//******************************************************************************************************
//  MainForm.cs - Gbtc
//
//  Copyright © 2021, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  12/30/2020 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Soap;
using System.Windows.Forms;
using GSF;
using GSF.Communication;
using GSF.ComponentModel;
using GSF.Diagnostics;
using GSF.IO;
using GSF.PhasorProtocols;
using GSF.Windows.Forms;

// ReSharper disable LocalizableElement
#pragma warning disable IDE1006 // Naming Styles

namespace SELPDCImporter
{
    public partial class MainForm : Form
    {
        private Settings m_settings;
        private readonly LogPublisher m_log;
        private string m_hostApp;
        private bool m_formLoaded;
        private bool m_initialShow;
        private bool m_formClosing;
        private bool m_analyzeInProgress;
        private bool m_connectionStringManuallyEdited;
        private ImportParameters m_importParams;

        public MainForm()
        {
            InitializeComponent();

            // Create a new log publisher instance
            m_log = Logger.CreatePublisher(typeof(MainForm), MessageClass.Application);
            m_initialShow = true;
        }

        private string HostApp
        {
            get => m_hostApp;
            set
            {
                m_hostApp = value;
                Text = string.IsNullOrWhiteSpace(m_hostApp) ? Tag.ToString() : $"{Tag} => Targeting \"{m_hostApp}\"";
            }
        }

        private void SELPDCImporter_Load(object sender, EventArgs e)
        {
            try
            {
                // Save original title text in form tag
                Tag = Text;

                // Save original label text in its tag
                labelAnalyzeStatus.Tag = labelAnalyzeStatus.Text;

                // Load current settings registering a symbolic reference to this form instance for use by default value expressions
                m_settings = new Settings(new Dictionary<string, object> {{ "Form", this }}.RegisterSymbols());

                // Restore last window size/location
                this.RestoreLayout();

                m_importParams = new ImportParameters();

                if (string.IsNullOrWhiteSpace(m_settings.HostConfig) || !File.Exists(m_settings.HostConfig))
                    m_settings.HostConfig = LookupHostConfig();

                m_formLoaded = true;
            }
            catch (Exception ex)
            {
                m_log.Publish(MessageLevel.Error, "FormLoad", exception: ex);

            #if DEBUG
                throw;
            #else
                MessageBox.Show(this, $"Failed during initialization: {ex.Message}", "Initialization Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            #endif
            }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (!m_initialShow || m_formClosing)
                return;

            m_initialShow = false;

            if (string.IsNullOrWhiteSpace(textBoxHostConfig.Text))
                textBoxHostConfig.Focus();
            else
                textBoxPDCConfig.Focus();
        }

        private void SELPDCImporter_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                m_formClosing = true;

                // Save current window size/location
                this.SaveLayout();

                // Save any updates to current screen values
                m_settings.Save();

                // Close any active database connection
                m_importParams?.Dispose();

                // Close associated console process
                StopConsoleProcess();
            }
            catch (Exception ex)
            {
                m_log.Publish(MessageLevel.Error, "FormClosing", exception: ex);

            #if DEBUG
                throw;
            #endif
            }
        }

        private void SELPDCImporter_FormClosed(object sender, FormClosedEventArgs e)
        {
            m_settings?.Dispose();
        }

        private void FormElementChanged(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, EventArgs>(FormElementChanged), sender, e);
            }
            else
            {
                if (Visible && m_formLoaded)
                    m_settings?.UpdateProperties();
            }
        }

        private void textBoxPDCConfig_TextChanged(object sender, EventArgs e)
        {
            buttonAnalyze.Enabled = true;
            buttonReview.Enabled = false;
            buttonTestConnection.Enabled = false;

            labelAnalyzeStatus.Text = $"{labelAnalyzeStatus.Tag}";
            textBoxPDCDetails.Text = "";
            textBoxConnectionString.Text = "";

            m_connectionStringManuallyEdited = false;
            comboBoxIPAddresses.DataSource = null;
            comboBoxIPAddresses.Items.Clear();
        }

        private void buttonBrowseHostConfig_Click(object sender, EventArgs e)
        {
            openFileDialogHostConfig.FileName = textBoxHostConfig.Text;

            if (openFileDialogHostConfig.ShowDialog(this) != DialogResult.OK)
                return;

            textBoxHostConfig.Text = openFileDialogHostConfig.FileName;
        }

        private void buttonBrowsePDCConfig_Click(object sender, EventArgs e)
        {
            openFileDialogPDCConfig.FileName = textBoxPDCConfig.Text;

            if (openFileDialogPDCConfig.ShowDialog(this) != DialogResult.OK)
                return;

            textBoxPDCConfig.Text = openFileDialogPDCConfig.FileName;

            string hostConfigFile = textBoxHostConfig.Text;

            // Auto analyze after PDC config file selection assuming host config looks OK
            if (File.Exists(hostConfigFile) && IsHostConfig(hostConfigFile))
                buttonAnalyze_Click(sender, e);
        }

        private void buttonAnalyze_Click(object sender, EventArgs e)
        {
            Ticks startTime = DateTime.UtcNow.Ticks;

            try
            {
                m_analyzeInProgress = true;
                Cursor.Current = Cursors.WaitCursor;

                string hostConfigFile = textBoxHostConfig.Text;

                if (!File.Exists(hostConfigFile))
                {
                    MessageBox.Show(this, $"Analyze failed: The specified host service configuration file \"{Path.GetFileName(hostConfigFile)}\" does not exist.", "Load Host Config File Issue", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string pdcConfigFile = textBoxPDCConfig.Text;

                if (!File.Exists(pdcConfigFile))
                {
                    MessageBox.Show(this, $"Analyze failed: The specified PDC configuration file \"{Path.GetFileName(pdcConfigFile)}\" does not exist.", "Load PDC Config File Issue", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!IsHostConfig(hostConfigFile))
                {
                    MessageBox.Show(this, $"Analyze failed: The configuration file \"{Path.GetFileName(hostConfigFile)}\" is not a valid host service configuration.", "Load Host Config File Issue", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    LoadHostConfigFile(hostConfigFile, m_importParams);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Analyze failed: Initialization failure using specified host service configuration \"{Path.GetFileName(hostConfigFile)}\": {ex.Message}", "Load Host Config File Issue", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    m_importParams.SELPDCConfigFrame = SELPDCConfig.Parse(pdcConfigFile, (message, title, buttons, icon) => MessageBox.Show(this, message, title, buttons, icon));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Analyze failed: Failed while parsing PDC configuration \"{Path.GetFileName(pdcConfigFile)}\": {ex.Message}", "Load PDC Config File Issue", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ConfigurationFrame configFrame = m_importParams.SELPDCConfigFrame;

                // Show PDC hierarchy
                textBoxPDCDetails.Text = configFrame.GeneratePDCDetails();

                // Initialize IP address drop-down from device IP dictionary
                comboBoxIPAddresses.DataSource = new BindingSource(configFrame.DeviceIPs, null);
                comboBoxIPAddresses.ValueMember = "Key";
                comboBoxIPAddresses.DisplayMember = "Value";

                if (!string.IsNullOrWhiteSpace(configFrame.TargetDeviceIP))
                    comboBoxIPAddresses.SelectedValue = configFrame.TargetDeviceIP;

                // Reset manually edited state flag for connection string
                m_connectionStringManuallyEdited = false;

                // Enable button sequence based on parse success
                buttonTestConnection.Enabled = buttonReview.Enabled = configFrame.Cells.Count > 0;
                buttonAnalyze.Enabled = !buttonReview.Enabled;
            }
            finally
            {
                m_analyzeInProgress = false;
                Cursor.Current = Cursors.Default;
            }

            // Provide user feedback on analyze operation completion
            labelAnalyzeStatus.Text = $"Analyze completed in {(DateTime.UtcNow.Ticks - startTime).ToElapsedTimeString(2)}.";
        }

        private void buttonReview_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                buttonReview.Enabled = false;
                buttonReview.Refresh();

                try
                {
                    m_importParams.GSFPDCConfigFrame = GSFPDCConfig.Extract(m_importParams.Connection, m_importParams.SELPDCConfigFrame.IDCode, textBoxConnectionString.Text.ParseDeviceIPFromConnectionString());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Analyze failed: Failed while attempting to load existing GSF configuration from database connection as defined in host service configuration \"{Path.GetFileName(textBoxHostConfig.Text)}\": {ex.Message}", "Load GSF Database Config Issue", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                EditDetails editDetails = new() { ImportParams = m_importParams };

                if (editDetails.ShowDialog(this) == DialogResult.Cancel)
                    return;

                ConfigurationCell[] cells = m_importParams.TargetConfigFrame.Cells.Cast<ConfigurationCell>().ToArray();

                if (cells.Length == 0)
                {
                    MessageBox.Show(this, "Import Canceled: No PMU devices defined for import.", "Import Canceled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Cursor.Current = Cursors.WaitCursor;
                m_importParams.HostConfig = textBoxHostConfig.Text;
                m_importParams.EditedConnectionString = textBoxConnectionString.Text;

                GSFPDCConfig.SaveConnection(m_importParams);

                // Initialize new connection from host service
                m_consoleProcess?.StandardInput.WriteLine($"init {m_importParams.TargetConfigFrame.Acronym}");

                string summaryMessage;
                int deletedCount = cells.Count(cell => cell.Delete);

                if (deletedCount > 0)
                {
                    summaryMessage = deletedCount == cells.Length ? 
                        $"Successfully deleted {deletedCount:N0} PMU devices." : 
                        $"Successfully imported {m_importParams.TargetConfigFrame.Cells.Count - deletedCount:N0} and deleted {deletedCount:N0} PMU devices.";
                }
                else
                {
                    summaryMessage = $"Successfully imported {m_importParams.TargetConfigFrame.Cells.Count:N0} PMU devices.";
                }

                MessageBox.Show(this, summaryMessage, "PDC Import Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                m_consoleProcess?.StandardInput.WriteLine($"connect {m_importParams.TargetConfigFrame.Acronym}");

                // Clear PDC config text to ready for next
                BeginInvoke(new Action(() => textBoxPDCConfig.Text = ""));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import failed: Failed while importing PDC configuration: {ex.Message}", "Import PDC Config Issue", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                buttonReview.Enabled = true;
                Cursor.Current = Cursors.Default;
            }
        }

        private void textBoxConnectionString_TextChanged(object sender, EventArgs e)
        {
            if (!m_analyzeInProgress)
                m_connectionStringManuallyEdited = true;
        }

        private void comboBoxIPAddresses_TextChanged(object sender, EventArgs e)
        {
            string connectionString = m_importParams?.SELPDCConfigFrame?.ConnectionString;

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                if (m_connectionStringManuallyEdited && MessageBox.Show(this, "Manual changes to connection string are about to be overwritten, continue?", "Update Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                textBoxConnectionString.Text = connectionString.Replace(SELPDCConfig.IPAddressToken, comboBoxIPAddresses.Text);
                m_connectionStringManuallyEdited = false;
            }
        }

        private void comboBoxIPAddresses_SelectedIndexChanged(object sender, EventArgs e)
        {
            string connectionString = m_importParams?.SELPDCConfigFrame?.ConnectionString;

            if (!string.IsNullOrWhiteSpace(connectionString) && comboBoxIPAddresses.SelectedItem is KeyValuePair<string, string> kvp)
            {
                if (m_connectionStringManuallyEdited && MessageBox.Show(this, "Manual changes to connection string are about to be overwritten, continue?", "Update Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                textBoxConnectionString.Text = connectionString.Replace(SELPDCConfig.IPAddressToken, kvp.Value);
                m_connectionStringManuallyEdited = false;
            }
        }

        private void buttonTestConnection_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(textBoxConnectionString.Text))
                {
                    MessageBox.Show(this, "No connection string defined, cannot launch PMU Connection Tester.", "Missing Connection String", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ConfigurationFrame configFrame = m_importParams.SELPDCConfigFrame;

                if (configFrame is null)
                {
                    MessageBox.Show(this, "No configuration data has been analyzed, cannot launch PMU Connection Tester.", "Missing Configuration Frame", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Dictionary<string, string> settings = textBoxConnectionString.Text.ParseKeyValuePairs();

                if (!settings.TryGetValue(nameof(PhasorProtocol), out string setting) || !Enum.TryParse(setting, out PhasorProtocol phasorProtocol))
                {
                    MessageBox.Show(this, "Failed to parse phasor protocol from connection string, cannot launch PMU Connection Tester.", "Connection String Parse Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                settings.Remove(nameof(PhasorProtocol));

                if (!settings.TryGetValue(nameof(TransportProtocol), out setting) || !Enum.TryParse(setting, out TransportProtocol transportProtocol))
                {
                    MessageBox.Show(this, "Failed to parse transport protocol from connection string, cannot launch PMU Connection Tester.", "Connection String Parse Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                settings.Remove(nameof(TransportProtocol));

                ConnectionSettings connectionSettings = new()
                {
                    PhasorProtocol = phasorProtocol,
                    TransportProtocol = transportProtocol,
                    ConnectionString = settings.JoinKeyValuePairs(),
                    PmuID = configFrame.IDCode,
                    FrameRate = configFrame.FrameRate,
                    AutoRepeatPlayback = false,
                    ByteEncodingDisplayFormat = 0,
                    ConnectionParameters = null
                };

                SoapFormatter formatter = new()
                {
                    AssemblyFormat = FormatterAssemblyStyle.Simple,
                    TypeFormat = FormatterTypeStyle.TypesWhenNeeded
                };

                string fileName = Path.Combine(FilePath.GetApplicationDataFolder(), $"{configFrame.Acronym}.PmuConnection");
                using FileStream settingsFile = File.Create(fileName);
                formatter.Serialize(settingsFile, connectionSettings);
                settingsFile.Close();

                Process.Start(fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to launch PMU Connection Tester: {ex.Message}", "External Tool Launch Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
