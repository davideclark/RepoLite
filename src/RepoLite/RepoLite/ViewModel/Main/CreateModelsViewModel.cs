﻿using Newtonsoft.Json;
using RepoLite.Commands;
using RepoLite.Common;
using RepoLite.Common.Enums;
using RepoLite.Common.Models;
using RepoLite.DataAccess;
using RepoLite.GeneratorEngine;
using RepoLite.GeneratorEngine.Generators.BaseParsers;
using RepoLite.GeneratorEngine.Generators.BaseParsers.Base;
using RepoLite.GeneratorEngine.Models;
using RepoLite.ViewModel.Base;
using RepoLite.Views;
using RepoLite.Views.Generation;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace RepoLite.ViewModel.Main
{
    public class CreateModelsViewModel : ViewModelBase
    {
        private bool _loaded;
        public ObservableCollection<string> Messages { get; set; } = new ObservableCollection<string>();

        public ObservableCollection<TableToGenerate> Tables { get; set; } = new ObservableCollection<TableToGenerate>();

        public bool Loaded
        {
            get => _loaded;
            set => SetProperty(ref _loaded, value);
        }

        public ICommand LoadTables
        {
            get
            {
                return new RelayCommand(o =>
                {
                    Tables.Clear();
                    var dataSource = DataSource.GetDataSource();
                    var tableDefinitions = dataSource.GetTables();
                    foreach (var table in tableDefinitions.OrderBy(x => x.Schema).ThenBy(x => x.Table).ToList())
                    {
                        Tables.Add(new TableToGenerate { Schema = table.Schema, Table = table.Table });
                    }
                    OnPropertyChanged(nameof(Tables));
                    Loaded = true;
                });
            }
        }

        public ICommand ToggleSelectAll
        {
            get
            {
                return new RelayCommand(o =>
                {
                    var shouldSelect = Math.Abs(Math.Round(Tables.Count(x => x.Selected) / (float) Tables.Count)) < 0.001;
                    foreach (var table in Tables)
                    {
                        table.Selected = shouldSelect;
                    }
                }, o => Loaded);
            }
        }

        public ICommand Generate
        {
            get
            {
                return new RelayCommand(o =>
                {
                    var tables = Tables.Where(x => x.Selected);

                    var dataSource = DataSource.GetDataSource();
                    var tableDefinitions = dataSource.LoadTables(tables.Select(x => new TableAndSchema(x.Schema, x.Table)).ToList());

                    var generator = CodeGenerator.GetGenerator();
                    var outputDirectory = AppSettings.Generation.OutputDirectory.TrimEnd('/').TrimEnd('\\');

                    if (!Directory.Exists(outputDirectory))
                        Directory.CreateDirectory(outputDirectory);

                    DoWork(() =>
                    {
                        CreateBaseModel(outputDirectory, generator);

                        tableDefinitions.ForEach(x =>
                        {
                            LogMessage($"Processing Table {x.Schema}.{x.ClassName}");
                            var model = generator.ModelForTable(x).ToString();

                            CreateModel(x, outputDirectory, generator, model);
                        });
                    }, () => Process.Start(outputDirectory));
                });
            }
        }

        public ICommand SaveSelection
        {
            get
            {
                return new RelayCommand(o =>
                {
                    var dlg = new InputDialog();
                    dlg.ShowDialog();
                    var f = dlg.Value;

                    if (!Directory.Exists($"{App.ClientDataPath}TableSelections"))
                        Directory.CreateDirectory($"{App.ClientDataPath}TableSelections");

                    var presets = new DirectoryInfo($"{App.ClientDataPath}TableSelections").GetFiles();

                    if (presets.All(x => x.Name.Split('.')[0] != f))
                    {
                        var tableSelection = new TableSelection
                        {
                            Name = f,
                            SelectedTables = Tables.Where(x => x.Selected).Select(x => $"{x.Schema}.{x.Table}").ToList()
                        };

                        var json = JsonConvert.SerializeObject(tableSelection);

                        File.WriteAllText($@"{App.ClientDataPath}TableSelections\{f}.json", json);
                    }
                    else
                    {
                        LogMessage("Template already exists");
                    }
                });
            }
        }

        public ICommand LoadSelection
        {
            get
            {
                return new RelayCommand(o =>
                {
                    var dlg = new LoadTemplates();
                    dlg.ShowDialog();
                    var f = dlg.SelectedItem;

                    if (!Directory.Exists($"{App.ClientDataPath}TableSelections"))
                        Directory.CreateDirectory($"{App.ClientDataPath}TableSelections");

                    var presets = new DirectoryInfo($"{App.ClientDataPath}TableSelections").GetFiles();

                    var template = presets.FirstOrDefault(x => x.Name.Split('.')[0] == f);
                    if (template == null) return;

                    var obj = JsonConvert.DeserializeObject<TableSelection>(File.ReadAllText(template.FullName));

                    foreach (var objSelectedTable in obj.SelectedTables)
                    {
                        var table = Tables.FirstOrDefault(x => $"{x.Schema}.{x.Table}" == objSelectedTable);
                        if (table != null)
                            table.Selected = true;
                    }
                });
            }
        }

        internal void CreateModel(Table table, string outputDirectory, IGenerator generator, string modelName)
        {
            var result = Regex.Replace(
                AppSettings.Generation.ModelFileNameFormat,
                Regex.Escape("{Name}"),
                table.ClassName.Replace("$", "$$"),
                RegexOptions.IgnoreCase
            );

            result = Regex.Replace(
                result,
                Regex.Escape("{Schema}"),
                table.Schema.Replace("$", "$$"),
                RegexOptions.IgnoreCase
            );

            string fileName;

            switch (AppSettings.System.GenerationLanguage)
            {
                case GenerationLanguage.CSharp:
                    fileName = $"{outputDirectory}/{result}.{generator.FileExtension()}";
                    break;
                default:
                    //"If you've added a new language to the enum, the generator needs creating and hooking up here"
                    throw new ArgumentOutOfRangeException();
            }

            if (!Directory.Exists(AppSettings.Generation.OutputDirectory))
                Directory.CreateDirectory(AppSettings.Generation.OutputDirectory);
            LogMessage($"Creating Model File for {table.Schema}.{table.ClassName} in {outputDirectory}/");
            File.WriteAllText(fileName, modelName);
            LogMessage($"Done {table.Schema}.{table.ClassName}!");
        }

        internal void CreateBaseModel(string outputDirectory, IGenerator generator)
        {
            if (!Directory.Exists($"{outputDirectory}/Base"))
                Directory.CreateDirectory($"{outputDirectory}/Base");

            IParser parser;

            switch (AppSettings.System.GenerationLanguage)
            {
                case GenerationLanguage.CSharp:
                    parser = new CSharpSqlServerBaseClassParser();
                    break;
                default:
                    //"If you've added a new language to the enum, the generator needs creating and hooking up here"
                    throw new ArgumentOutOfRangeException();
            }

            //Write base model
            var baseModel = parser.BuildBaseModel();
            LogMessage($"Creating Base Model file in {outputDirectory}/Base/");
            File.WriteAllText($"{outputDirectory}/Base/BaseModel.{generator.FileExtension()}", baseModel);
            LogMessage("Created Base Model file.");
        }

        private void LogMessage(string message)
        {
            AddToList(message, Messages);
        }
    }
}
