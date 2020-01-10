using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using AForge.Imaging.Filters;

namespace ThreadPool
{
    public class View: INotifyPropertyChanged
    {
        public ObservableCollection<string> FileList { get; set; }
        public bool Observating { get; set; }
        public bool Processing { get; set; }
        public string PathIn { get; set; }
        public string PathOut { get; set; }
        public string Error { get; set; }
        public string Logs { get; set; }
        public string CurrentFile { get; set; }
        public int Percent { get; set; }
        public bool Edges { get; set; }
        public bool Erosion { get; set; }
        public bool Adaptive { get; set; }
        public bool Oil { get; set; }
        public bool Jitter { get; set; }

        public View()
        {
            PathIn = PathOut = Error = Logs = CurrentFile = "";
            Observating = Processing = false;
            Edges = Erosion = Adaptive = Oil = Jitter = false;
            FileList = new ObservableCollection<string>();
            Percent = 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow
    {
        private FileSystemWatcher _fileSystemWatcher;
        private readonly View _view;
        private Bitmap _image;

        public MainWindow()
        {
            InitializeComponent();
            _view = new View();
            _image = new Bitmap(10,10);
            DataContext = _view;
            System.Threading.ThreadPool.QueueUserWorkItem(Process);
        }

        private void Edges_Filter_Process(object callback)
        {
            var filter = new Edges();
            AddLog("filtr Edges na " + _view.CurrentFile);
            filter.ApplyInPlace(_image);
            Thread.Sleep(200);
            ((AutoResetEvent)callback).Set();
        }

        private void Erosion_Filter_Process(object callback)
        {
            var filter = new Erosion();
            AddLog("filtr Erosion  na " + _view.CurrentFile);
            filter.ApplyInPlace(_image);
            Thread.Sleep(200);
            ((AutoResetEvent)callback).Set();
        }

        private void Adaptive_Filter_Process(object callback)
        {
            var filter = new AdaptiveSmoothing();
            AddLog("filtr AdaptiveSmoothing na " + _view.CurrentFile);
            filter.ApplyInPlace(_image);
            Thread.Sleep(200);
            ((AutoResetEvent)callback).Set();
        }

        private void Oil_Filter_Process(object callback)
        {
            var filter = new OilPainting(15);
            AddLog("filtr OilPainting na " + _view.CurrentFile);
            filter.ApplyInPlace(_image);
            Thread.Sleep(200);
            ((AutoResetEvent)callback).Set();
        }

        private void Jitter_Filter_Process(object callback)
        {
            var filter = new Jitter(4);
            AddLog("filtr Jitter na " + _view.CurrentFile);
            filter.ApplyInPlace(_image);
            Thread.Sleep(200);
            ((AutoResetEvent)callback).Set();
        }

        private void Process(object callback)
        {
            var autoEvent = new AutoResetEvent(false);
            var filters = new bool[5];
            var done = 0;
            

            while (true)
            {
                Thread.Sleep(500);
                _view.Percent = 0;
                _view.OnPropertyChanged("percent");

                while (_view.FileList.Count > 0)
                {
                    while (!_view.Processing)
                    {
                        Thread.Sleep(200);
                    }

                    filters[0] = _view.Edges;
                    filters[1] = _view.Erosion;
                    filters[2] = _view.Adaptive;
                    filters[3] = _view.Oil;
                    filters[4] = _view.Jitter;

                    try
                    {
                        _view.CurrentFile = _view.FileList[0];
                        _view.OnPropertyChanged("currentfile");
                    }
                    catch (Exception e)
                    {
                        SetError(e.Message);
                    }
                    
                    _image = new Bitmap(_view.CurrentFile);
                    AddLog("Przetwarzanie " + _view.CurrentFile);

                    try
                    {
                        if (filters[0])
                        {
                            System.Threading.ThreadPool.QueueUserWorkItem(Edges_Filter_Process,autoEvent);
                            autoEvent.WaitOne();
                        }
                        if (filters[1])
                        {
                            System.Threading.ThreadPool.QueueUserWorkItem(Erosion_Filter_Process, autoEvent);
                            autoEvent.WaitOne();
                        }
                        if (filters[2])
                        {
                            System.Threading.ThreadPool.QueueUserWorkItem(Adaptive_Filter_Process, autoEvent);
                            autoEvent.WaitOne();
                        }
                        if (filters[3])
                        {
                            System.Threading.ThreadPool.QueueUserWorkItem(Oil_Filter_Process, autoEvent);
                            autoEvent.WaitOne();
                        }
                        if (filters[4])
                        {
                            System.Threading.ThreadPool.QueueUserWorkItem(Jitter_Filter_Process, autoEvent);
                            autoEvent.WaitOne();
                        }

                        _image.Save(Path.Combine(_view.PathOut, Path.GetFileName(_view.CurrentFile)));
                        Dispatcher?.Invoke((Action) (() => _view.FileList.RemoveAt(0)));
                        
                    }
                    catch (Exception e)
                    {
                        SetError(e.Message);
                    }

                    done += 1;
                    _view.Percent = done * 100 / (_view.FileList.Count + done);
                    _view.OnPropertyChanged("percent");
                }

                _view.CurrentFile = "";
                _view.OnPropertyChanged("currentfile");
            }
        }

        private static string ChooseDirectory()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(fbd.SelectedPath))
                    return fbd.SelectedPath;
            }

            return "";
        }

        private void Input_Button_Click(object sender, RoutedEventArgs e)
        {
            _view.PathIn = ChooseDirectory();
            _view.OnPropertyChanged("pathIn");
        }

        private void Output_Button_Click(object sender, RoutedEventArgs e)
        {
            _view.PathOut = ChooseDirectory();
            _view.OnPropertyChanged("pathOut");
        }

        public void SetError(string text)
        {
            _view.Error = text;
            _view.OnPropertyChanged("error");
        }

        public void AddLog(string text)
        {
            _view.Logs = text + "\n" + _view.Logs;
            _view.OnPropertyChanged("logs");
        }

        private void Start_observing_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!_view.Observating && _view.FileList.Count == 0)
            {
                if (string.IsNullOrEmpty(_view.PathIn) || string.IsNullOrEmpty(_view.PathOut) || !Directory.Exists(_view.PathIn) ||
                    !Directory.Exists(_view.PathOut))
                {
                    SetError("Wybierz poprawny folder plików.");
                    return;
                }

                _view.Observating = true;
                SetError("");
                AddLog("Obserwowanie " + _view.PathIn);
                _fileSystemWatcher = new FileSystemWatcher(_view.PathIn);
                _fileSystemWatcher.Created += FileSystemWatcher_Created;

                var files = Directory.GetFiles(_view.PathIn);
                foreach (string file in files)
                {
                    _view.FileList.Add(file);
                    AddLog(file + " dodano do kolejki.");
                }

                _fileSystemWatcher.EnableRaisingEvents = true;
            }
            else
                SetError("Obserwowany folder został już wybrany.");
            
        }

        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            Dispatcher?.Invoke((Action) (() =>
            {
                _view.FileList.Add(e.FullPath);
                AddLog(e.FullPath + " dodano do kolejki.");
            }));
        }

        private void Stop_observing_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!_view.Observating) return;
            _view.Observating = false;
            _fileSystemWatcher.EnableRaisingEvents = false;
            AddLog("Obserwowanie " + _view.PathIn + " zakończone.");
        }

        private void Start_processing_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_view.Processing) return;
            _view.Processing = true;
            AddLog("Rozpoczęto proces.");
        }

        private void Stop_processing_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!_view.Processing) return;
            _view.Processing = false;
            AddLog("Zakończono proces.");
        }

        private void Cancel_processing_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!_view.Processing) return;
            _view.Percent = 0;
            _view.Processing = false;
            _view.FileList.Clear();
            AddLog("Wycofano proces.");
        }
    }
}
