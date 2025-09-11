using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Text;
using Microsoft.Win32;

namespace RevitAddinDiagnostic
{
    public partial class DiagnosticForm : Form
    {
        private TextBox txtResults;
        private Button btnCheckAddin;
        private Button btnCheckEnvironment;
        private Button btnCheckRevit;
        private Button btnSaveReport;
        private OpenFileDialog openFileDialog;

        public DiagnosticForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Revit Add-in Diagnostic Tool";
            this.Size = new System.Drawing.Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create controls
            txtResults = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new System.Drawing.Font("Consolas", 9),
                ReadOnly = true,
                Dock = DockStyle.Fill
            };

            var panel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top
            };

            btnCheckAddin = new Button
            {
                Text = "Kiểm tra Add-in",
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(120, 30)
            };

            btnCheckEnvironment = new Button
            {
                Text = "Kiểm tra Môi trường",
                Location = new System.Drawing.Point(140, 10),
                Size = new System.Drawing.Size(120, 30)
            };

            btnCheckRevit = new Button
            {
                Text = "Kiểm tra Revit",
                Location = new System.Drawing.Point(270, 10),
                Size = new System.Drawing.Size(120, 30)
            };

            btnSaveReport = new Button
            {
                Text = "Lưu Report",
                Location = new System.Drawing.Point(400, 10),
                Size = new System.Drawing.Size(100, 30)
            };

            openFileDialog = new OpenFileDialog
            {
                Filter = "Add-in files (*.addin)|*.addin|DLL files (*.dll)|*.dll|All files (*.*)|*.*",
                Title = "Chọn file Add-in hoặc DLL"
            };

            // Add event handlers
            btnCheckAddin.Click += BtnCheckAddin_Click;
            btnCheckEnvironment.Click += BtnCheckEnvironment_Click;
            btnCheckRevit.Click += BtnCheckRevit_Click;
            btnSaveReport.Click += BtnSaveReport_Click;

            // Add controls to form
            panel.Controls.Add(btnCheckAddin);
            panel.Controls.Add(btnCheckEnvironment);
            panel.Controls.Add(btnCheckRevit);
            panel.Controls.Add(btnSaveReport);

            this.Controls.Add(txtResults);
            this.Controls.Add(panel);

            // Auto check environment on load
            this.Load += (s, e) => BtnCheckEnvironment_Click(s, e);
        }

        private void BtnCheckAddin_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                CheckAddinFile(openFileDialog.FileName);
            }
        }

        private void BtnCheckEnvironment_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== KIỂM TRA MÔI TRƯỜNG ===");
            sb.AppendLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine();

            // Check .NET Framework
            sb.AppendLine("--- .NET Framework ---");
            try
            {
                var version = Environment.Version;
                sb.AppendLine($"CLR Version: {version}");
                
                var netFxVersion = Get45PlusFromRegistry();
                sb.AppendLine($".NET Framework: {netFxVersion}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Lỗi kiểm tra .NET: {ex.Message}");
            }

            // Check Windows version
            sb.AppendLine();
            sb.AppendLine("--- Windows ---");
            sb.AppendLine($"OS Version: {Environment.OSVersion}");
            sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");

            // Check current directory and permissions
            sb.AppendLine();
            sb.AppendLine("--- Thư mục và Quyền ---");
            sb.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
            sb.AppendLine($"User: {Environment.UserName}");
            sb.AppendLine($"Machine: {Environment.MachineName}");

            // Check GAC and loaded assemblies
            sb.AppendLine();
            sb.AppendLine("--- Assemblies đã load ---");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.Contains("Revit") || assembly.FullName.Contains("Autodesk"))
                {
                    sb.AppendLine($"  {assembly.FullName}");
                }
            }

            txtResults.Text = sb.ToString();
        }

        private void BtnCheckRevit_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== KIỂM TRA REVIT ===");
            sb.AppendLine();

            // Check Revit installations
            sb.AppendLine("--- Revit đã cài đặt ---");
            var revitVersions = GetInstalledRevitVersions();
            foreach (var version in revitVersions)
            {
                sb.AppendLine($"  {version.Key}: {version.Value}");
            }

            // Check Revit Add-ins folders
            sb.AppendLine();
            sb.AppendLine("--- Thư mục Add-ins ---");
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var addinsBasePath = Path.Combine(appDataPath, "Autodesk", "Revit", "Addins");
            
            if (Directory.Exists(addinsBasePath))
            {
                var versions = Directory.GetDirectories(addinsBasePath);
                foreach (var versionDir in versions)
                {
                    var versionName = Path.GetFileName(versionDir);
                    sb.AppendLine($"  Revit {versionName}: {versionDir}");
                    
                    // Check .addin files in this version
                    var addinFiles = Directory.GetFiles(versionDir, "*.addin");
                    foreach (var addinFile in addinFiles)
                    {
                        sb.AppendLine($"    - {Path.GetFileName(addinFile)}");
                    }
                }
            }
            else
            {
                sb.AppendLine("  Không tìm thấy thư mục Add-ins");
            }

            txtResults.Text = sb.ToString();
        }

        private void CheckAddinFile(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== KIỂM TRA ADD-IN: {Path.GetFileName(filePath)} ===");
            sb.AppendLine($"Đường dẫn: {filePath}");
            sb.AppendLine();

            try
            {
                if (filePath.EndsWith(".addin", StringComparison.OrdinalIgnoreCase))
                {
                    CheckAddinManifest(filePath, sb);
                }
                else if (filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    CheckDllFile(filePath, sb);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"LỖI: {ex.Message}");
                sb.AppendLine($"Stack trace: {ex.StackTrace}");
            }

            txtResults.Text = sb.ToString();
        }

        private void CheckAddinManifest(string addinPath, StringBuilder sb)
        {
            sb.AppendLine("--- Kiểm tra file .addin ---");
            
            if (!File.Exists(addinPath))
            {
                sb.AppendLine("❌ File không tồn tại");
                return;
            }

            try
            {
                var content = File.ReadAllText(addinPath);
                sb.AppendLine("✅ File đọc được");
                
                // Parse XML
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(content);
                
                sb.AppendLine("✅ XML hợp lệ");
                
                // Check AddIn nodes
                var addInNodes = doc.SelectNodes("//AddIn");
                sb.AppendLine($"Số lượng AddIn: {addInNodes.Count}");
                
                foreach (System.Xml.XmlNode addIn in addInNodes)
                {
                    var name = addIn.SelectSingleNode("Name")?.InnerText ?? "N/A";
                    var assembly = addIn.SelectSingleNode("Assembly")?.InnerText ?? "N/A";
                    var className = addIn.SelectSingleNode("FullClassName")?.InnerText ?? "N/A";
                    
                    sb.AppendLine();
                    sb.AppendLine($"AddIn: {name}");
                    sb.AppendLine($"Assembly: {assembly}");
                    sb.AppendLine($"Class: {className}");
                    
                    // Check if assembly path is absolute or relative
                    if (Path.IsPathRooted(assembly))
                    {
                        sb.AppendLine("📍 Đường dẫn tuyệt đối");
                        if (File.Exists(assembly))
                        {
                            sb.AppendLine("✅ Assembly tồn tại");
                            CheckDllFile(assembly, sb);
                        }
                        else
                        {
                            sb.AppendLine("❌ Assembly không tồn tại");
                        }
                    }
                    else
                    {
                        sb.AppendLine("📍 Đường dẫn tương đối");
                        var addinDir = Path.GetDirectoryName(addinPath);
                        var fullAssemblyPath = Path.Combine(addinDir, assembly);
                        
                        if (File.Exists(fullAssemblyPath))
                        {
                            sb.AppendLine($"✅ Assembly tồn tại: {fullAssemblyPath}");
                            CheckDllFile(fullAssemblyPath, sb);
                        }
                        else
                        {
                            sb.AppendLine($"❌ Assembly không tồn tại: {fullAssemblyPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Lỗi parse .addin: {ex.Message}");
            }
        }

        private void CheckDllFile(string dllPath, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine($"--- Kiểm tra DLL: {Path.GetFileName(dllPath)} ---");
            
            if (!File.Exists(dllPath))
            {
                sb.AppendLine("❌ DLL không tồn tại");
                return;
            }

            try
            {
                // Basic file info
                var fileInfo = new FileInfo(dllPath);
                sb.AppendLine($"Kích thước: {fileInfo.Length:N0} bytes");
                sb.AppendLine($"Ngày tạo: {fileInfo.CreationTime:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine($"Ngày sửa: {fileInfo.LastWriteTime:dd/MM/yyyy HH:mm:ss}");

                // Try to load assembly for reflection
                var assembly = Assembly.LoadFrom(dllPath);
                sb.AppendLine("✅ Assembly load thành công");
                
                sb.AppendLine($"Full name: {assembly.FullName}");
                sb.AppendLine($"Location: {assembly.Location}");
                sb.AppendLine($"Target Framework: {assembly.ImageRuntimeVersion}");

                // Check for Revit dependencies
                sb.AppendLine();
                sb.AppendLine("--- Dependencies ---");
                var referencedAssemblies = assembly.GetReferencedAssemblies();
                foreach (var refAssembly in referencedAssemblies)
                {
                    if (refAssembly.Name.Contains("Revit") || refAssembly.Name.Contains("Autodesk"))
                    {
                        sb.AppendLine($"  📦 {refAssembly.FullName}");
                    }
                }

                // Check for IExternalCommand implementations
                sb.AppendLine();
                sb.AppendLine("--- IExternalCommand Classes ---");
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var interfaces = type.GetInterfaces();
                    foreach (var iface in interfaces)
                    {
                        if (iface.Name.Contains("IExternalCommand") || iface.Name.Contains("IExternalApplication"))
                        {
                            sb.AppendLine($"  🎯 {type.FullName} implements {iface.Name}");
                        }
                    }
                }

                // Check dependencies existence
                sb.AppendLine();
                sb.AppendLine("--- Kiểm tra Dependencies ---");
                var dllDir = Path.GetDirectoryName(dllPath);
                foreach (var refAssembly in referencedAssemblies)
                {
                    if (!refAssembly.Name.StartsWith("System") && !refAssembly.Name.StartsWith("Microsoft"))
                    {
                        var depPath = Path.Combine(dllDir, refAssembly.Name + ".dll");
                        if (File.Exists(depPath))
                        {
                            sb.AppendLine($"  ✅ {refAssembly.Name}.dll");
                        }
                        else
                        {
                            sb.AppendLine($"  ❌ {refAssembly.Name}.dll (thiếu)");
                        }
                    }
                }

            }
            catch (ReflectionTypeLoadException ex)
            {
                sb.AppendLine($"❌ Lỗi load types: {ex.Message}");
                sb.AppendLine("Loader exceptions:");
                foreach (var loaderEx in ex.LoaderExceptions)
                {
                    if (loaderEx != null)
                        sb.AppendLine($"  - {loaderEx.Message}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Lỗi kiểm tra DLL: {ex.Message}");
            }
        }

        private Dictionary<string, string> GetInstalledRevitVersions()
        {
            var versions = new Dictionary<string, string>();
            
            try
            {
                // Check registry for Revit installations
                var registryPaths = new[]
                {
                    @"SOFTWARE\Autodesk\Revit",
                    @"SOFTWARE\WOW6432Node\Autodesk\Revit"
                };

                foreach (var regPath in registryPaths)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(regPath))
                    {
                        if (key != null)
                        {
                            foreach (var subKeyName in key.GetSubKeyNames())
                            {
                                using (var subKey = key.OpenSubKey(subKeyName))
                                {
                                    var installLocation = subKey?.GetValue("InstallLocation") as string;
                                    if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                                    {
                                        versions[$"Revit {subKeyName}"] = installLocation;
                                    }
                                }
                            }
                        }
                    }
                }

                // Also check common installation paths
                var commonPaths = new[]
                {
                    @"C:\Program Files\Autodesk",
                    @"C:\Program Files (x86)\Autodesk"
                };

                foreach (var basePath in commonPaths)
                {
                    if (Directory.Exists(basePath))
                    {
                        var revitDirs = Directory.GetDirectories(basePath, "Revit *");
                        foreach (var dir in revitDirs)
                        {
                            var dirName = Path.GetFileName(dir);
                            if (!versions.ContainsKey(dirName))
                            {
                                versions[dirName] = dir;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                versions["Error"] = ex.Message;
            }

            return versions;
        }

        private string Get45PlusFromRegistry()
        {
            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

            using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
            {
                if (ndpKey?.GetValue("Release") != null)
                {
                    return CheckFor45PlusVersion((int)ndpKey.GetValue("Release"));
                }
            }
            return "Không tìm thấy .NET Framework 4.5+";
        }

        private string CheckFor45PlusVersion(int releaseKey)
        {
            if (releaseKey >= 533320) return "4.8.1 hoặc mới hơn";
            if (releaseKey >= 528040) return "4.8";
            if (releaseKey >= 461808) return "4.7.2";
            if (releaseKey >= 461308) return "4.7.1";
            if (releaseKey >= 460798) return "4.7";
            if (releaseKey >= 394802) return "4.6.2";
            if (releaseKey >= 394254) return "4.6.1";
            if (releaseKey >= 393295) return "4.6";
            if (releaseKey >= 379893) return "4.5.2";
            if (releaseKey >= 378675) return "4.5.1";
            if (releaseKey >= 378389) return "4.5";
            return "Không xác định được version";
        }

        private void BtnSaveReport_Click(object sender, EventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"RevitAddinDiagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(saveDialog.FileName, txtResults.Text);
                MessageBox.Show($"Report đã được lưu: {saveDialog.FileName}", "Thành công", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DiagnosticForm());
        }
    }
}
