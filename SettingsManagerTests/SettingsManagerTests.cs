using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using GlobalSettingsManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace SettingsManagerTests
{

    public class CustomSetting
    {
        public string Text { get; set; }
        public CustomSetting Ref { get; set; }

        public override bool Equals(object obj)
        {
            var o = obj as CustomSetting;
            if (o != null)
            {
                return Text == o.Text && ((Ref == o.Ref) || Ref.Equals(o.Ref));
            }
            return false;
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }


    public class Settings : SelfManagedSettings<Settings>
    {
        public override string Category
        {
            get { return "Settings"; }
        }

        public DateTime Time { get; set; }
        public decimal Decimal { get; set; }
        public float Float { get; set; }
        public ConsoleColor Enum { get; set; }
        public bool Boolean { get; set; }
        public int Integer { get; set; }
        public UInt64 BigInteger { get; set; }
        public char Character { get; set; }
        public string Text { get; set; }
        public TimeSpan TimeSpan { get; set; }
        public TimeSpan TimeSpan2 { get; set; }
        public CustomSetting Custom { get; set; }
    }

    public class ReadOnlySettings : SelfManagedSettings<ReadOnlySettings>
    {
        public override bool ReadOnly
        {
            get { return true; }
        }
        public override string Category
        {
            get { return "Settings2"; }
        }
        public string Text { get; set; }
    }

    [TestClass]
    public class SettingsManagerTests
    {
        private readonly List<SettingsStorageModel> _mockSettingsStorageModels;

        public SettingsManagerTests()
        {
            _mockSettingsStorageModels = new List<SettingsStorageModel>();
        }

        [TestMethod]
        public void BasicReading()
        {
            var repo = new InMemoryRepository();
            repo.Content.Add(new SettingsStorageModel() { Category = "Settings", Name = "Decimal", Value = "1.5" });
            repo.Content.Add(new SettingsStorageModel() { Category = "Settings", Name = "Text", Value = "test" });

            SettingsManager.DefaultManagerInstance = new SettingsManagerPeriodic(repo);
            var settings = Settings.Get();

            Assert.AreEqual(1.5m, settings.Decimal);
            Assert.AreEqual("test", settings.Text);
        }

        [TestMethod]
        public void SavingAndLoadingMustBeSame()
        {
            var repo = new InMemoryRepository();
            var manager = new SettingsManagerPeriodic(repo);
            var settings = Settings.Get(customSettingsManager: manager);

            var custom = new CustomSetting()
            {
                Text = "1",
                Ref = new CustomSetting() { Text = "2" }
            };

            settings.Decimal = 0.01m;
            settings.Text = "test";
            settings.BigInteger = UInt64.MaxValue;
            settings.Boolean = true;
            settings.Character = 'X';
            settings.Enum = ConsoleColor.Green;
            settings.Float = float.MaxValue;
            settings.Integer = int.MinValue;
            settings.Time = DateTime.MaxValue;
            settings.TimeSpan = TimeSpan.MaxValue;
            settings.TimeSpan2 = new TimeSpan(0, 1, 2, 3);
            settings.Custom = custom;

            settings.Save();
            settings = Settings.Get(true, manager);

            Assert.AreEqual(0.01m, settings.Decimal);
            Assert.AreEqual("test", settings.Text);
            Assert.AreEqual(UInt64.MaxValue, settings.BigInteger);
            Assert.AreEqual(true, settings.Boolean);
            Assert.AreEqual('X', settings.Character);
            Assert.AreEqual(ConsoleColor.Green, settings.Enum);
            Assert.AreEqual(float.MaxValue, settings.Float);
            Assert.AreEqual(int.MinValue, settings.Integer);
            Assert.AreEqual(0, (int)(DateTime.MaxValue - settings.Time).TotalSeconds); //compare only to seconds precision
            Assert.AreEqual(TimeSpan.MaxValue, settings.TimeSpan);
            Assert.AreEqual(new TimeSpan(0, 1, 2, 3), settings.TimeSpan2);
            Assert.AreEqual(custom, settings.Custom);
        }

        [TestMethod]
        public void EmptyStringsMustWork()
        {
            var repo = new InMemoryRepository();
            var manager = new SettingsManagerPeriodic(repo);
            var settings = Settings.Get(customSettingsManager: manager);

            settings.Text = "";
            settings.Save();
            settings = Settings.Get(true, manager);
            Assert.AreEqual("", settings.Text);
        }


        [TestMethod]
        public void PeriodicReaderCancel()
        {
            var repo = new InMemoryRepository();
            var manager = new SettingsManagerPeriodic(repo);
            var settings = Settings.Get(customSettingsManager: manager);
            var cts = new CancellationTokenSource();
            var task = manager.StartReadingTask(TimeSpan.FromMilliseconds(1), cts.Token);
            int cnt = 0;
            bool cancelEvent = false;
            manager.PeriodicReaderExecuting += (sender, args) => cnt++;
            manager.PeriodicReaderCanceled += (sender, args) => cancelEvent = true;

            Thread.Sleep(200);
            cts.Cancel();
            Thread.Sleep(200);
            Assert.IsTrue(task.IsCanceled);
            Assert.IsTrue(cancelEvent);
            Assert.IsTrue(cnt > 0);
        }

        [TestMethod]
        public void ReadOnlyPropertyMustBeRespected()
        {
            var repo = new InMemoryRepository();
            SettingsManager.DefaultManagerInstance = new SettingsManagerPeriodic(repo);

            var settings = ReadOnlySettings.Get();

            settings.Text = "a";
            settings.Save();
            settings = ReadOnlySettings.Get(true);
            Assert.AreNotEqual("a", settings.Text);

            settings.ChangeAndSave(s => s.Text = "b");
            settings = ReadOnlySettings.Get(true);
            Assert.AreNotEqual("b", settings.Text);

        }

        [TestMethod]
        public void ShouldCheckIfFlagIsSetOnNonEmptyFlagsCollection()
        {
            Mock<ISettingsRepository> settingsRepo = new Mock<ISettingsRepository>();

            _mockSettingsStorageModels.AddRange(new List<SettingsStorageModel>{
                new SettingsStorageModel
                {
                    Category = SettingsManager.FlagsCategoryName,
                    Name = "BiddingSystem",
                    Value = "true",
                    UpdatedAt = new DateTime(2015, 1, 1)
                },
                new SettingsStorageModel
                {
                    Category = SettingsManager.FlagsCategoryName,
                    Name = "BannerBaseAddress",
                    Value = "1",
                    UpdatedAt = new DateTime(2015, 2, 1)

                }});

            settingsRepo.Setup(x => x.ReadSettings(It.IsAny<string>()))
                .Returns<string>(category => _mockSettingsStorageModels
                    .Where(x => x.Category == category));

            SettingsManager.DefaultManagerInstance = new SettingsManager(settingsRepo.Object);
            var settingsManager = (SettingsManager)SettingsManager.DefaultManagerInstance;

            Assert.AreEqual(true, settingsManager.IsFlagSet("BiddingSystem"));
        }

        [TestMethod]
        public void ShouldFailWhenInvalidFlagTypesExistOnNonEmptyFlagsCollection()
        {
            Mock<ISettingsRepository> settingsRepo = new Mock<ISettingsRepository>();

            _mockSettingsStorageModels.AddRange(new List<SettingsStorageModel>{
                new SettingsStorageModel
                {
                    Category = SettingsManager.FlagsCategoryName,
                    Name = "ServiceBaseAddress",
                    Value = "abbv",
                    UpdatedAt = new DateTime(2015, 1, 1)
                },
                new SettingsStorageModel
                {
                    Category = SettingsManager.FlagsCategoryName,
                    Name = "StartCount",
                    Value = "12",
                    UpdatedAt = new DateTime(2015, 2, 1)

                }});

            settingsRepo.Setup(x => x.ReadSettings(It.IsAny<string>()))
                .Returns<string>(category => _mockSettingsStorageModels
                    .Where(x => x.Category == category));

            SettingsManager.DefaultManagerInstance = new SettingsManager(settingsRepo.Object);
            var settingsManager = (SettingsManager)SettingsManager.DefaultManagerInstance;

            Assert.AreEqual(false, settingsManager.IsFlagSet("ServiceBaseAddress"));
            Assert.AreEqual(false, settingsManager.IsFlagSet("StartCount"));
        }

        [TestMethod]
        public void FlagShouldNotBeSetOnEmptyFlagsCollection()
        {
            Mock<ISettingsRepository> settingsRepo = new Mock<ISettingsRepository>();

            settingsRepo.Setup(x => x.ReadSettings(It.IsAny<string>()))
                .Returns<string>(category => new List<SettingsStorageModel>().Where(x => x.Category == category));

            SettingsManager.DefaultManagerInstance = new SettingsManager(settingsRepo.Object);
            var settingsManager = (SettingsManager)SettingsManager.DefaultManagerInstance;

            Assert.AreEqual(false, settingsManager.IsFlagSet("BiddingSystem"));
        }

        [TestMethod]
        public void PeriodicReaderShouldReactToFlagChanges()
        {
            Mock<ISettingsRepository> settingsRepo = new Mock<ISettingsRepository>();

            _mockSettingsStorageModels.AddRange(new List<SettingsStorageModel> {
                new SettingsStorageModel
                {
                    Category = SettingsManager.FlagsCategoryName,
                    Name = "X",
                    Value = "true",
                    UpdatedAt = new DateTime(2015, 1, 1)
                }
            });

            settingsRepo.Setup(x => x.ReadSettings(It.IsAny<string>()))
               .Returns<string>(category => _mockSettingsStorageModels);

            settingsRepo.Setup(x => x.ReadSettings(It.IsAny<IList<string>>(), It.IsAny<DateTime?>()))
               .Returns<IList<string>, DateTime?>((categories, lastChangedDate) => _mockSettingsStorageModels);
            var cancellationTokenSource = new CancellationTokenSource();

            SettingsManager.DefaultManagerInstance = new SettingsManagerPeriodic(settingsRepo.Object);
            var manager = (SettingsManagerPeriodic)SettingsManager.DefaultManagerInstance;

            var initialValue = manager.IsFlagSet("X");
            manager.StartReadingTask(TimeSpan.FromMilliseconds(10), cancellationTokenSource.Token);
            Thread.Sleep(100);
            var repoItem = _mockSettingsStorageModels.Single(s => s.Name == "X");
            repoItem.Value = "0";
            Thread.Sleep(100);
            Assert.AreEqual(!initialValue, manager.IsFlagSet("X"));
        }

        [TestMethod]
        public void PreriodicReaderMustRaiseError()
        {
            var cts = new CancellationTokenSource();
            var repo = new InMemoryRepository();
            SettingsManager.DefaultManagerInstance = new SettingsManagerPeriodic(repo);
            var settings = ReadOnlySettings.Get();
            settings.Text = "a";
            settings.Save();

            var manager = (SettingsManager.DefaultManagerInstance as SettingsManagerPeriodic);
            var error = false;
            manager.PeriodicReaderError += (s, a) => { error = true; };
            manager.StartReadingTask(TimeSpan.FromMilliseconds(10), cts.Token);
            Thread.Sleep(200);
            Assert.AreEqual(false, error);
        }

        [TestMethod]
        public void PeriodicErrorEventManagerShouldCollectThrownExceptions()
        {

        }
    }
}
