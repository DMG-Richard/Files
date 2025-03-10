using Files.App.Extensions;
using Files.App.Filesystem;
using Files.App.Helpers;
using CommunityToolkit.WinUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Microsoft.UI.Dispatching;

namespace Files.App.ViewModels.Properties
{
    internal class CombinedProperties : BaseProperties
    {
        public List<ListedItem> List { get; }

        public CombinedProperties(SelectedItemsPropertiesViewModel viewModel, CancellationTokenSource tokenSource,
            DispatcherQueue coreDispatcher, List<ListedItem> listedItems, IShellPage instance)
        {
            ViewModel = viewModel;
            TokenSource = tokenSource;
            Dispatcher = coreDispatcher;
            List = listedItems;
            AppInstance = instance;
            GetBaseProperties();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        public override void GetBaseProperties()
        {
            if (List != null)
            {
                ViewModel.LoadCombinedItemsGlyph = true;
                if (List.All(x => x.ItemType.Equals(List.First().ItemType)))
                {
                    ViewModel.ItemType = string.Format("PropertiesDriveItemTypesEquals".GetLocalizedResource(), List.First().ItemType);
                }
                else
                {
                    ViewModel.ItemType = "PropertiesDriveItemTypeDifferent".GetLocalizedResource();
                }
                var itemsPath = List.Select(Item => (Item as RecycleBinItem)?.ItemOriginalFolder ??
                    (Path.IsPathRooted(Item.ItemPath) ? Path.GetDirectoryName(Item.ItemPath) : Item.ItemPath));
                if (itemsPath.Distinct().Count() == 1)
                {
                    ViewModel.ItemPath = string.Format("PropertiesCombinedItemPath".GetLocalizedResource(), itemsPath.First());
                }
            }
        }

        public override async void GetSpecialProperties()
        {
            if (List.All(x => x.PrimaryItemAttribute == StorageItemTypes.File))
            {
                ViewModel.IsReadOnly = List.All(x => NativeFileOperationsHelper.HasFileAttribute(x.ItemPath, System.IO.FileAttributes.ReadOnly));
            }
            ViewModel.IsHidden = List.All(x => NativeFileOperationsHelper.HasFileAttribute(x.ItemPath, System.IO.FileAttributes.Hidden));

            ViewModel.LastSeparatorVisibility = false;
            ViewModel.ItemSizeVisibility = true;

            ViewModel.FilesCount += List.Where(x => x.PrimaryItemAttribute == StorageItemTypes.File).ToList().Count;
            ViewModel.FoldersCount += List.Where(x => x.PrimaryItemAttribute == StorageItemTypes.Folder).ToList().Count;

            long totalSize = 0;
            long filesSize = List.Where(x => x.PrimaryItemAttribute == StorageItemTypes.File).Sum(x => x.FileSizeBytes);
            long foldersSize = 0;

            ViewModel.ItemSizeProgressVisibility = true;
            foreach (var item in List)
            {
                if (item.PrimaryItemAttribute == StorageItemTypes.Folder)
                {
                    var fileSizeTask = Task.Run(async () =>
                    {
                        var size = await CalculateFolderSizeAsync(item.ItemPath, TokenSource.Token);
                        return size;
                    });
                    try
                    {
                        foldersSize += await fileSizeTask;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Warn(ex, ex.Message);
                    }
                }
            }
            ViewModel.ItemSizeProgressVisibility = false;

            totalSize = filesSize + foldersSize;
            ViewModel.ItemSize = totalSize.ToLongSizeString();
            SetItemsCountString();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "IsReadOnly":
                    if (ViewModel.IsReadOnly)
                    {
                        List.ForEach(x => NativeFileOperationsHelper.SetFileAttribute(
                            x.ItemPath, System.IO.FileAttributes.ReadOnly));
                    }
                    else
                    {
                        List.ForEach(x => NativeFileOperationsHelper.UnsetFileAttribute(
                            x.ItemPath, System.IO.FileAttributes.ReadOnly));
                    }
                    break;

                case "IsHidden":
                    if (ViewModel.IsHidden)
                    {
                        List.ForEach(x => NativeFileOperationsHelper.SetFileAttribute(
                            x.ItemPath, System.IO.FileAttributes.Hidden));
                    }
                    else
                    {
                        List.ForEach(x => NativeFileOperationsHelper.UnsetFileAttribute(
                            x.ItemPath, System.IO.FileAttributes.Hidden));
                    }
                    break;
            }
        }
    }
}