using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Channels;
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


    public class Settings : SettingsBase
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

    public class ReadOnlySettings : SettingsBase
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

            var manager = new SettingsManagerPeriodic(repo);
            var settings = manager.Get<Settings>();

            Assert.AreEqual(1.5m, settings.Decimal);
            Assert.AreEqual("test", settings.Text);
        }


        [TestMethod]
        public void PeriodicReaderMustDetectNewFlags()
        {
            var cts = new CancellationTokenSource();
            var repo = new InMemoryRepository();
            var manager = new SettingsManagerPeriodic(repo);
            Assert.AreEqual(false, manager.IsFlagSet("TestFlag"));
            manager.StartReadingTask(TimeSpan.FromMilliseconds(10), cts.Token);
            repo.Content.Add(new SettingsStorageModel() { Category = SettingsManager.FlagsCategoryName, Name = "TestFlag", Value = "True" });
            Thread.Sleep(200);
            Assert.AreEqual(true, manager.IsFlagSet("TestFlag"));
            cts.Cancel();
        }

        [TestMethod]
        public void SavingAndLoadingMustBeSame()
        {
            var repo = new InMemoryRepository();
            var manager = new SettingsManagerPeriodic(repo);
            var settings = manager.Get<Settings>();

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

            manager.Save(settings);
            settings = manager.Get<Settings>(true);

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
            var settings = manager.Get<Settings>();

            settings.Text = "";
            manager.Save(settings);
            settings = manager.Get<Settings>();
            Assert.AreEqual("", settings.Text);
        }


        [TestMethod]
        public void PeriodicReaderCancel()
        {
            var repo = new InMemoryRepository();
            var manager = new SettingsManagerPeriodic(repo);
            var settings = manager.Get<Settings>();
            var cts = new CancellationTokenSource();
            var task = manager.StartReadingTask(TimeSpan.FromMilliseconds(1), cts.Token);
            int cnt = 0;
            bool cancelEvent = false;
            manager.PeriodicReaderExecuting += (sender, args) => cnt++;
            manager.PeriodicReaderCanceled += (sender, args) => cancelEvent = true;

            Thread.Sleep(200);
            cts.Cancel();
            Thread.Sleep(200);
            Assert.IsTrue(cancelEvent);
            Assert.IsTrue(task.IsCanceled);
            Assert.IsTrue(cnt > 0);
        }

        [TestMethod]
        public void ReadOnlyPropertyMustBeRespected()
        {
            var repo = new InMemoryRepository();
            var manager = new SettingsManagerPeriodic(repo);
            var settings = manager.Get<ReadOnlySettings>();
            settings.Text = "a";
            manager.Save(settings);
            settings = manager.Get<ReadOnlySettings>(true);
            Assert.AreNotEqual("a", settings.Text);

            manager.ChangeAndSave(s => s.Text = "b", settings);
            settings = manager.Get<ReadOnlySettings>(true);
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

            var manager = new SettingsManager(settingsRepo.Object);
            Assert.AreEqual(true, manager.IsFlagSet("BiddingSystem"));
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

            var manager = new SettingsManagerPeriodic(settingsRepo.Object);
            Assert.AreEqual(false, manager.IsFlagSet("ServiceBaseAddress"));
            Assert.AreEqual(false, manager.IsFlagSet("StartCount"));
        }

        [TestMethod]
        public void FlagShouldNotBeSetOnEmptyFlagsCollection()
        {
            Mock<ISettingsRepository> settingsRepo = new Mock<ISettingsRepository>();

            settingsRepo.Setup(x => x.ReadSettings(It.IsAny<string>()))
                .Returns<string>(category => new List<SettingsStorageModel>().Where(x => x.Category == category));

            var manager = new SettingsManagerPeriodic(settingsRepo.Object);
            Assert.AreEqual(false, manager.IsFlagSet("BiddingSystem"));
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

            var manager = new SettingsManagerPeriodic(settingsRepo.Object);

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
            var manager = new SettingsManagerPeriodic(repo);
            var settings = manager.Get<ReadOnlySettings>();
            settings.Text = "a";
            manager.Save(settings);

            var error = false;
            manager.PeriodicReaderError += (s, a) => { error = true; };
            manager.StartReadingTask(TimeSpan.FromMilliseconds(10), cts.Token);
            Thread.Sleep(200);
            Assert.AreEqual(false, error);
        }

        //[TestMethod]
        //public void PeriodicErrorEventManagerShouldCaptureRepeatingExceptions()
        //{
        //    var cts = new CancellationTokenSource();
        //    Mock<ISettingsRepository> settingsRepo = new Mock<ISettingsRepository>();

        //    settingsRepo.Setup(x => x.ReadSettings(It.IsAny<IList<string>>(), It.IsAny<DateTime?>()))
        //        .Returns<IList<string>, DateTime?>((categories, lastChangedDate) => { throw new Exception(); });
        //    var manager = new SettingsManagerPeriodic(settingsRepo.Object);

        //    var error = false;
        //    RepeatingErrorEventArgs repeatingErrorEventArgs = null;

        //    manager.PeriodicReaderError += (sender, eventArgs) =>
        //    {
        //        error = true;
        //        repeatingErrorEventArgs = eventArgs;
        //    };
        //    manager.StartReadingTask(TimeSpan.FromMilliseconds(20), cts.Token);
        //    Thread.Sleep(100);

        //    Assert.AreEqual(repeatingErrorEventArgs.Exception.GetType(), typeof(Exception));
        //    Assert.IsTrue(repeatingErrorEventArgs.IsRepeating);
        //    Assert.AreEqual(true, error);
        //}

        //[TestMethod]
        //public void RepeatingExceptionsShouldBeFlushedAfterParticularPeriodOfTime()
        //{
        //    var cts = new CancellationTokenSource();

        //    Mock<ISettingsRepository> settingsRepo = new Mock<ISettingsRepository>();

        //    settingsRepo.Setup(x => x.ReadSettings(It.IsAny<IList<string>>(), It.IsAny<DateTime?>()))
        //        .Returns<IList<string>, DateTime?>((categories, lastChangedDate) => { throw new Exception(); });
        //    var manager = new SettingsManagerPeriodic(settingsRepo.Object);
        //    manager.RepeatingErrorInterval = TimeSpan.FromMilliseconds(50);

        //    int repeating = 0, nonRepeating = 0;

        //    manager.PeriodicReaderError += (sender, eventArgs) =>
        //    {
        //        if (eventArgs.IsRepeating)
        //            repeating++;
        //        else
        //        {
        //            nonRepeating++;
        //        }
        //    };

        //    manager.StartReadingTask(TimeSpan.Zero, cts.Token);
        //    Thread.Sleep(200);

        //    Assert.IsTrue(repeating > 1);
        //    Assert.IsTrue( nonRepeating > 1);
        //}

        [TestMethod]
        public void SettingsManagerShouldCollectSetPropertyExceptions()
        {
            var inMemoryRepository = new InMemoryRepository();
            inMemoryRepository.Content.Add(new SettingsStorageModel() { Category = "Settings", Name = "Decimal", Value = "test" });
            inMemoryRepository.Content.Add(new SettingsStorageModel() { Category = "Settings", Name = "Text", Value = "test" });

            var manager = new SettingsManagerPeriodic(inMemoryRepository);

            bool propertyErrorOccurred = false;

            manager.PropertyError += (sender, eventArgs) =>
            {
                propertyErrorOccurred = eventArgs.IsRepeating;
            };

            var settings = manager.Get<Settings>(true);
                
            Assert.IsTrue(propertyErrorOccurred);
        }
    }
}
