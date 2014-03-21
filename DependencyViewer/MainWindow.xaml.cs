using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DependencyViewer.Common;
using DependencyViewer.Common.Graphing;
using DependencyViewer.Common.Loaders;
using DependencyViewer.Common.Model;
using DependencyViewer.Properties;
using System.Collections.Generic;

namespace DependencyViewer
{
	public partial class MainWindow
	{
	    private Solution _currentSolution;
        private Solution _allSolution;
        private readonly Dictionary<Guid, Project> _projects_in_sln = new Dictionary<Guid, Project>();
		public MainWindow()
		{
			InitializeComponent();

			SetupGraphVizPath();
		}

		private static void SetupGraphVizPath()
		{
			if (string.IsNullOrEmpty(Settings.Default.GraphVizPath) == false && File.Exists(Settings.Default.GraphVizPath)) 
				return;

            // Search for the likely location of graphviz
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
		    string folderToStartIn = programFiles;
            
            var folders = Directory.GetDirectories(programFiles, "Graphviz*")
                .Select(f => Path.Combine(f, "bin"))
                .Where(f => Directory.GetFiles(f, "dot.exe").Length > 0)
                .OrderBy(k => k);

            if (folders.Count() != 0)
            {
                folderToStartIn = folders.Last();
            }

			MessageBox.Show("Please set the location of dot.exe. It should be in the Graphviz bin directory.");
			var ofd = new System.Windows.Forms.OpenFileDialog
			              {
			                  CheckFileExists = true,
			                  Multiselect = false,
                              InitialDirectory = folderToStartIn
			              };

		    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				Settings.Default.GraphVizPath = ofd.FileName;
				Settings.Default.Save();
			}
			else
			{
				MessageBox.Show("Cannot continue without Graphviz, exiting...");
				Application.Current.Shutdown(0);
			}
		}

		private void FilenameBrowseButton_Click(object sender, RoutedEventArgs e)
		{
			var ofd = new System.Windows.Forms.OpenFileDialog
			              {
			                  CheckFileExists = true,
			                  Multiselect = false,
			                  InitialDirectory = Directory.GetCurrentDirectory()
			              };

		    if(ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				tbFilename.Text = ofd.FileName;
			}
		}

		private void OutputBrowseButton_Click(object sender, RoutedEventArgs e)
		{
			var ofd = new System.Windows.Forms.OpenFileDialog
			              {
			                  Multiselect = false,
			                  InitialDirectory = Directory.GetCurrentDirectory()
			              };

		    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				tbOutputFilename.Text = ofd.FileName;
			}
		}

		private void tbFilename_TextChanged(object sender, TextChangedEventArgs e)
		{
			bool fileExists = FilenameIsValid();
			((TextBox)sender).Background = new SolidColorBrush(fileExists ? Colors.White : Colors.Red);
			btnRender.IsEnabled = fileExists;
            
            LoadSolution();
            
		}

	    private bool FilenameIsValid()
	    {
	        return File.Exists(tbFilename.Text);
	    }

	    private void LoadSolution()
	    {
            lbProjects.Items.Clear();
            _projects_in_sln.Clear();
            if (FilenameIsValid() == false) return;

	        SolutionLoader loader = GetLoader();
	        _currentSolution = new Solution(loader);
            _allSolution = new Solution(loader);

            foreach (var project in _currentSolution.Projects)
            {
                lbProjects.Items.Add(project);
                _projects_in_sln.Add(project.ProjectIdentifier,project);
            }
            
            _currentSolution.Clear();
          
	    }

	    private SolutionLoader GetLoader()
	    {
	        var solutionLoader = new SolutionLoader(File.ReadAllText(tbFilename.Text), tbFilename.Text);
            solutionLoader.LoadProjects();

	        return solutionLoader;
	    }

	    private void btnRender_Click(object sender, RoutedEventArgs e)
		{
            if (_currentSolution == null) return;
 
            var processor = new QuickGraphProcessor(_currentSolution);
            processor.SetAllSolution(_allSolution);
			Compose(processor);
			string filename = processor.ProcessSolution();

			var graphViz = new GraphVizService();
			graphViz.ExecGraphViz(filename, tbOutputFilename.Text);

			Process.Start(tbOutputFilename.Text);
		}

		private static void Compose(QuickGraphProcessor processor)
		{
			var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var container = new CompositionContainer(new DirectoryCatalog(directory));
			container.ComposeParts(processor);
		}

        private void ProjectSelection_Click(object sender, RoutedEventArgs e)
        {
            Project project = (sender as CheckBox).DataContext as Project;
            CheckBox check_box = sender as CheckBox;
            if (check_box.IsChecked == true)
            {
                _currentSolution.Add(project);
            }
        }

        private void btnSaveDependencies_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSolution != null)
            {
                SaveDependencies("selected_dependencies.txt", _currentSolution.Projects, _currentSolution);
            }
        }

        private void SaveDependencies(string fileName,IList<Project> projects,Solution solution)
        {
            if (projects.Count < 0) return;

            FileStream fs = File.Create(fileName);
            StreamWriter sw = new StreamWriter(fs);
            int index = 0;
            int max_project_ref = 0;
            string max_ref_of_project = null;
            foreach (var project in projects)
            {
                index++;
                sw.WriteLine("project :" + index + "  " + project.Name);
                sw.WriteLine("--dependencies :");
                int project_ref_index = 0;

                foreach (var projectRef in project.ProjectReferences)
                {
                    if (solution.GetProject(projectRef) == null) continue;
                    project_ref_index++;
                    Project refproject = solution.GetProject(projectRef);
                    sw.WriteLine(index + "--" + project_ref_index + "--" + refproject.Name);
                }
                if (project_ref_index > max_project_ref)
                {
                    max_project_ref = project_ref_index;
                    max_ref_of_project = project.Name;
                }

                sw.WriteLine();
            }
            if (max_project_ref > 0)
              sw.WriteLine("Max project refs is " + max_project_ref + " of" + max_ref_of_project);
            sw.Close();
            fs.Close();
        }

        private void btnSaveAllDependencies_Click(object sender, RoutedEventArgs e)
        {
            if (_allSolution != null)
            {
                SaveDependencies("all_dependencies.txt", _allSolution.Projects, _allSolution);
            }
           
        }

	}
}
