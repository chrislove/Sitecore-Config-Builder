﻿namespace Sitecore.ConfigBuilder.Tool
{
  using System;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Windows.Forms;
  using System.Xml.Linq;
  using Sitecore.Diagnostics;
  using Sitecore.Diagnostics.Annotations;
  using Sitecore.Diagnostics.ConfigBuilder;
  using Sitecore.Diagnsotics.InformationService.Client;
  using System.Collections.Generic;
  using System.Runtime.Remoting.Messaging;

  internal partial class MainForm : Form
  {
    #region Fields and Constants

    private const string WebConfigOpenFilter = "*.config|*.config|Any file|*";

    private const string SaveFilter = "*.xml|*.xml|Any file|*";

    private const string ShowconfigFileName = "showconfig.xml";

    private const string WebConfigResultFileName = "web.config.result.xml";

    [NotNull]
    private static readonly string AppDataFolderPath = Environment.ExpandEnvironmentVariables("%APPDATA%\\Sitecore\\Sitecore.ConfigBuilder\\");

    [NotNull]
    private static readonly string SettingsFilePath = Path.Combine(AppDataFolderPath, "Settings.txt");

    #endregion

    public MainForm()
    {
      this.InitializeComponent();
    }

    #region Do Work

    private void SaveButton_Click([CanBeNull] object sender, [CanBeNull] EventArgs e)
    {
      try
      {
        var webConfigPath = this.FilePathTextbox.Text;
        Assert.IsNotNull(webConfigPath, "webConfigPath");

        var buildWebConfigResult = this.BuildWebConfigResult.Checked;
        var normalizeOutput = this.NormalizeOutput.Checked;

        var outputFile = this.GetShowConfigFilePath(ShowconfigFileName);
        if (string.IsNullOrEmpty(outputFile))
        {
          return;
        }

        Sitecore.Diagnostics.ConfigBuilder.ConfigBuilder.Build(webConfigPath, outputFile, false, normalizeOutput);

        if (buildWebConfigResult)
        {
          outputFile = this.GetShowConfigFilePath(WebConfigResultFileName);
          Assert.IsNotNull(outputFile, "outputFile");

          Sitecore.Diagnostics.ConfigBuilder.ConfigBuilder.Build(webConfigPath, outputFile, true, normalizeOutput);
        }

        var requireDefaultConfiguration = this.RequireDefaultConfiguration.Checked;
        if (requireDefaultConfiguration)
        {
          var version = this.SitecoreVersionComboBox.Text;
          if (!string.IsNullOrEmpty(version))
          {
            try
            {
              var tempFolder = SitecoreVersions.DownloadDefaultConfigurationFiles(version);
              try
              {
                if (tempFolder.Exists)
                {
                  Sitecore.Diagnostics.ConfigBuilder.ConfigBuilder.Build(Path.Combine(tempFolder.FullName, "web.config"), outputFile + "." + version + ".xml", false, normalizeOutput);
                  if (buildWebConfigResult)
                  {
                    Sitecore.Diagnostics.ConfigBuilder.ConfigBuilder.Build(Path.Combine(tempFolder.FullName, "web.config"), outputFile + "." + version + ".xml", true, normalizeOutput);
                  }
                }
              }
              finally
              {
                if (tempFolder.Exists)
                {
                  tempFolder.Delete();
                }
              }
            }
            catch (Exception ex)
            {
              // Log.Error()
            }
          }
        }

        if (this.OpenFolder.Checked && File.Exists(outputFile))
        {
          string argument = @"/select, """ + outputFile + @"""";
          Process.Start("explorer.exe", argument);
        }

        if (this.CloseWhenDone.Checked)
        {
          this.Close();
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show("The action failed with exception. " + ex.Message + Environment.NewLine + "Find details in the ConfigBuilder.ConfigBuilder.dll.log file", "Sitecore ConfigBuilder");
        File.AppendAllText("ConfigBuilder.ConfigBuilder.dll.log", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + " ERROR " + ex.GetType().FullName + Environment.NewLine + "Message: " + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace + Environment.NewLine);
      }
    }

    [CanBeNull]
    private string GetShowConfigFilePath([NotNull] string fileName, [CanBeNull] string webConfigFilePath = null)
    {
      Assert.ArgumentNotNull(fileName, "fileName");

      if (!this.NoDestinationPrompt.Checked)
      {
        var fileDialog = new SaveFileDialog
      {
        FileName = fileName,
        Filter = SaveFilter
      };

        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
          return null;
        }

        var result = fileDialog.FileName;
        Assert.IsNotNull(result, "fileName");

        return result;
      }

      var directoryName = Path.GetDirectoryName(webConfigFilePath ?? this.FilePathTextbox.Text);
      Assert.IsNotNull(directoryName, "directoryName");

      return Path.Combine(directoryName, fileName);
    }

    #endregion

    #region Drag'n'Drop

    private void MainForm_DragEnter([CanBeNull] object sender, [NotNull] DragEventArgs e)
    {
      Assert.ArgumentNotNull(e, "e");

      var data = e.Data.GetData(DataFormats.FileDrop);
      if (data == null)
      {
        return;
      }

      var filePaths = data as string[];
      if (filePaths == null)
      {
        return;
      }

      var filePath = filePaths[0];
      if (filePath.Length < 1)
      {
        return;
      }

      e.Effect = DragDropEffects.Link;
    }

    private void MainForm_DragDrop([CanBeNull] object sender, [NotNull] DragEventArgs e)
    {
      Assert.ArgumentNotNull(e, "e");

      var data = e.Data.GetData(DataFormats.FileDrop);
      if (data == null)
      {
        return;
      }

      var filePaths = data as string[];
      if (filePaths == null)
      {
        return;
      }

      if (filePaths.Length == 1)
      {
        this.FilePathTextbox.Text = filePaths[0];
        this.UpdateSaveButton();
      }
    }

    #endregion

    #region UI Logic

    private void MainForm_Load([CanBeNull] object sender, [CanBeNull] EventArgs e)
    {
      try
      {
        //new Action(() => PopulateVersionsComboBox()).BeginInvoke(null, null);
        new ToDoHandler(SitecoreVersions.GetVersions).BeginInvoke(PopulateVersionsComboBox, null);

        this.ParseCommandLine();

        this.ReadSettings();
        this.Text = string.Format(this.Text ?? string.Empty, GetVersion());
      }
      catch (Exception ex)
      {
        MessageBox.Show("The form load failed with exception. " + ex.Message + Environment.NewLine + Environment.NewLine + "Find details in log file");
        File.AppendAllText("ConfigBuilder.Tool.exe.log", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + " ERROR " + ex.GetType().FullName + Environment.NewLine + "Message: " + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace + Environment.NewLine);
      }
    }

    private void PopulateVersionsComboBox(IAsyncResult asyncRes)
    {
      var result1 = (AsyncResult)asyncRes;
      var result = ((ToDoHandler)result1.AsyncDelegate).EndInvoke(asyncRes);
      Action method = delegate
      {
        if (result != null)
        {
          object[] items = result.Cast<object>().ToArray<object>();
          this.SitecoreVersionComboBox.Items.AddRange(items);
          if (this.SitecoreVersionComboBox.Items.Count > 0)
          {
            this.SitecoreVersionComboBox.SelectedIndex = 0;
            if (this.FilePathTextbox.Text.Trim().Length > 0)
            {
              UpdateSaveButton();
            }
          }
        }
      };
      if (this.SitecoreVersionComboBox.InvokeRequired)
      {
        base.Invoke(method);
      }
      else
      {
        method();
      }
    }

    private delegate IEnumerable<string> ToDoHandler();
    
    [NotNull]
    private string GetVersion()
    {
      var assembly = Assembly.GetExecutingAssembly();
      var type = typeof(AssemblyFileVersionAttribute);
      var versionAttribute = assembly.GetCustomAttributes(type, true);
      if (versionAttribute.Length == 0)
      {
        return string.Empty;
      }

      var version = versionAttribute[0] as AssemblyFileVersionAttribute;
      return version != null ? version.Version ?? string.Empty : string.Empty;
    }

    private void ParseCommandLine()
    {
      var args = Environment.GetCommandLineArgs();
      if (args.Length != 2)
      {
        return;
      }

      this.FilePathTextbox.Text = args[1];
      this.UpdateSaveButton();
    }

    private void ReadSettings()
    {
      if (!File.Exists(SettingsFilePath))
      {
        return;
      }

      try
      {
        var boolParse = new Func<string[], int, bool>((p0, p1) =>
          {
            try
            {
              return bool.Parse(p0[p1]);
            }
            catch (Exception)
            {
              return false;
            }
          });

        var settings = File.ReadAllText(SettingsFilePath).Split('|');

        this.OpenFolder.Checked = boolParse(settings, 0);
        this.CloseWhenDone.Checked = boolParse(settings, 1);
        this.NormalizeOutput.Checked = boolParse(settings, 2);
        this.NoDestinationPrompt.Checked = boolParse(settings, 3);
        this.BuildWebConfigResult.Checked = boolParse(settings, 4);
      }
      catch (Exception)
      {
        if (File.Exists(SettingsFilePath))
        {
          File.Delete(SettingsFilePath);
        }
      }
    }

    private void UpdateSaveButton()
    {
      var webConfigFilePath = this.FilePathTextbox.Text.Trim();
      if (string.IsNullOrEmpty(webConfigFilePath))
      {
        this.SaveButton.Enabled = false;
        this.ErrorLabel.Text = @"The web.config file isn't chosen";
        return;
      }

      if (!File.Exists(webConfigFilePath))
      {
        this.SaveButton.Enabled = false;
        this.ErrorLabel.Text = @"The web.config file doesn't exist";
        return;
      }

      var websiteFolderPath = Path.GetDirectoryName(webConfigFilePath);
      var appConfigPath = Path.Combine(websiteFolderPath, "App_Config");
      if (!Directory.Exists(appConfigPath))
      {
        this.SaveButton.Enabled = false;
        this.ErrorLabel.Text = string.Format(@"The ""{0}"" folder doesn't exist", appConfigPath);
        return;
      }

      var version = TryDetectSitecoreVersion(websiteFolderPath);
      if (!string.IsNullOrEmpty(version))
      {
        foreach (var item in this.SitecoreVersionComboBox.Items)
        {
          if (item == null || item.ToString() != version)
          {
            continue;
          }

          this.SitecoreVersionComboBox.SelectedItem = item;
          break;
        }
      }

      this.SaveButton.Enabled = true;
      this.ErrorLabel.Text = @"Ready";
    }

    [CanBeNull]
    private string TryDetectSitecoreVersion([NotNull] string websiteFolderPath)
    {
      Assert.ArgumentNotNull(websiteFolderPath, "websiteFolderPath");

      try
      {
        var filePath = Path.Combine(websiteFolderPath, "sitecore.version.xml");
        if (!File.Exists(filePath))
        {
          var parentFolderPath = Path.GetDirectoryName(websiteFolderPath);
          Assert.IsNotNull(parentFolderPath, "parentFolderPath");

          filePath = Path.Combine(parentFolderPath, "sitecore.version.xml");
          if (!File.Exists(filePath))
          {
            var shellFolderPath = Path.Combine(websiteFolderPath, "sitecore\\shell");
            Assert.IsNotNull(shellFolderPath, "shellFolderPath");

            filePath = Path.Combine(shellFolderPath, "sitecore.version.xml");
            if (!File.Exists(filePath))
            {
              return null;
            }
          }
        }


        var doc = XDocument.Load(filePath);
        Assert.IsNotNull(doc, "doc");

        var major = doc.Descendants("major").FirstOrDefault().Value;
        var minor = doc.Descendants("minor").FirstOrDefault().Value;
        var build = doc.Descendants("build").FirstOrDefault().Value;
        var revision = doc.Descendants("revision").FirstOrDefault().Value;
        var version = major + "." + minor + (string.IsNullOrEmpty(build) ? "" : ("." + build)) + " rev. " + revision;

        return version;
      }
      catch
      {
      }

      return null;
    }

    private void SaveSettings([CanBeNull] object sender, [CanBeNull] EventArgs e)
    {
      if (!Directory.Exists(AppDataFolderPath))
      {
        Directory.CreateDirectory(AppDataFolderPath);
      }

      var file = Path.Combine(AppDataFolderPath, "Settings.txt");
      var contents = string.Format("{0}|{1}|{2}|{3}|{4}|{5}", OpenFolder.Checked, CloseWhenDone.Checked, NormalizeOutput.Checked, NoDestinationPrompt.Checked, BuildShowConfig.Checked, BuildWebConfigResult.Checked);
      File.WriteAllText(file, contents);
    }

    private void BrowseButton_Click([CanBeNull] object sender, [CanBeNull] EventArgs e)
    {
      try
      {
        var fileDialog = new OpenFileDialog { Filter = WebConfigOpenFilter };
        if (fileDialog.ShowDialog(this) == DialogResult.OK)
        {
          this.FilePathTextbox.Text = fileDialog.FileName;
        }

        this.UpdateSaveButton();
      }
      catch (Exception ex)
      {
        MessageBox.Show("The action failed with exception. " + ex.Message + Environment.NewLine + Environment.NewLine + "Find details in log file");
        File.AppendAllText("ConfigBuilder.Tool.exe.log", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + " ERROR " + ex.GetType().FullName + Environment.NewLine + "Message: " + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace + Environment.NewLine);
      }
    }

    private void FilePathTextbox_TextChanged([CanBeNull] object sender, [CanBeNull] EventArgs e)
    {
      try
      {
        this.UpdateSaveButton();
      }
      catch (Exception ex)
      {
        MessageBox.Show("The action failed with exception. " + ex.Message + Environment.NewLine + Environment.NewLine + "Find details in log file");
        File.AppendAllText("ConfigBuilder.Tool.exe.log", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + " ERROR " + ex.GetType().FullName + Environment.NewLine + "Message: " + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace + Environment.NewLine);
      }
    }

    #endregion

    private void NormalizeLink_Click([CanBeNull] object sender, [CanBeNull] LinkLabelLinkClickedEventArgs e)
    {
      try
      {
        var fileDialog = new OpenFileDialog { Filter = "Showconfig.xml file|*" };
        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
          return;
        }

        var showconfigPath = fileDialog.FileName;
        if (string.IsNullOrEmpty(showconfigPath))
        {
          return;
        }

        var outputFile = this.GetShowConfigFilePath(ShowconfigFileName, showconfigPath);
        if (string.IsNullOrEmpty(outputFile))
        {
          return;
        }

        Normalizer.Normalize(showconfigPath, showconfigPath + ".normalized.xml");

        if (this.OpenFolder.Checked && File.Exists(outputFile))
        {
          var argument = @"/select, """ + outputFile + @"""";
          Process.Start("explorer.exe", argument);
        }

        if (this.CloseWhenDone.Checked)
        {
          this.Close();
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show("The action failed with exception. " + ex.Message + Environment.NewLine + Environment.NewLine + "Find details in log file");
        File.AppendAllText("ConfigBuilder.Tool.exe.log", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + " ERROR " + ex.GetType().FullName + Environment.NewLine + "Message: " + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace + Environment.NewLine);
      }
    }
  }
}