/* -*- Mode: csharp; tab-width: 4; c-basic-offset: 4; indent-tabs-mode: t -*- */
/***************************************************************************
 *  IpodDap.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */
 
using System; 
using System.IO;
using System.Collections;
using Mono.Unix;
using Hal;
using IPod;

using Banshee.Base;
using Banshee.Dap;
 
namespace Banshee.Dap.Ipod
{
    [DapProperties(DapType = DapType.NonGeneric, PipelineName="Ipod")]
    [SupportedCodec(CodecType.Mp3)]
    [SupportedCodec(CodecType.Mp4)]
    public sealed class IpodDap : DapDevice
    {
        private IPod.Device device;
    
        public IpodDap(Hal.Device halDevice)
        {
            if(!halDevice.PropertyExists("block.device") || 
                !halDevice.GetPropertyBool("block.is_volume") ||
                halDevice.Parent["portable_audio_player.type"] != "ipod") {
                throw new CannotHandleDeviceException();
            } else if(!halDevice.GetPropertyBool("volume.is_mounted")) {
                throw new WaitForPropertyChangeException();
            }
            
            try {
                device = new IPod.Device(halDevice["block.device"]);
            } catch(Exception e) {
                throw new BrokenDeviceException(e.Message);
            }
            
            base.Initialize();
            
            InstallProperty("Generation", device.Generation.ToString());
            InstallProperty("Model", device.Model.ToString());
            InstallProperty("Model Number", device.ModelNumber);
            InstallProperty("Serial Number", device.SerialNumber);
            InstallProperty("Firmware Version", device.FirmwareVersion);
            InstallProperty("Database Version", device.SongDatabase.Version.ToString());
            
            ReloadDatabase(false);
            
            CanCancelSave = false;
        }
        
        protected override TrackInfo OnTrackAdded(TrackInfo track)
        {
            if(track is IpodDapTrackInfo) {
                return track;
            }

            if(!TrackExistsInList(track, device.SongDatabase.Songs)) {
                return new IpodDapTrackInfo(track, device.SongDatabase);
            }
            
            return null;
        }
        
        protected override void OnTrackRemoved(TrackInfo track)
        {
            if(!(track is IpodDapTrackInfo)) {
                return;
            }
            
            try {
                IpodDapTrackInfo ipod_track = (IpodDapTrackInfo)track;
                device.SongDatabase.RemoveSong(ipod_track.Song);
            } catch(Exception) {
            }
        }
        
        private void ReloadDatabase(bool refresh)
        {
            ClearTracks(false);
            
            if(refresh) {
                device.SongDatabase.Reload();
            }
            
            foreach(Song song in device.SongDatabase.Songs) {
                IpodDapTrackInfo track = new IpodDapTrackInfo(song);
                AddTrack(track);            
            }
        }
        
        public override void Eject()
        {
            device.Eject();
            base.Eject();
        }
        
        public override void Synchronize()
        {
            UpdateSaveProgress(
                Catalog.GetString("Synchronizing iPod"), 
                Catalog.GetString("Pre-processing tracks"),
                0.0);
            
            foreach(IpodDapTrackInfo track in Tracks) {
                if(track.Song == null) {
                    CommitTrackToDevice(track);
                } else {
                    track.Song.Uri = track.Uri;
                }
            }
            
            device.SongDatabase.SaveProgressChanged += delegate(object o, SaveProgressArgs args)
            {
                double progress = args.CurrentSong == null ? 0.0 : args.TotalProgress;
                string message = args.CurrentSong == null 
                    ? Catalog.GetString("Flushing to Disk (may take time)")
                    : args.CurrentSong.Artist + " - " + args.CurrentSong.Title;
                    
                UpdateSaveProgress(Catalog.GetString("Synchronizing iPod"), message, progress);
            };

            try {
                device.SongDatabase.Save();
            } catch(Exception e) {
                Console.Error.WriteLine (e);
                LogCore.Instance.PushError(Catalog.GetString("Failed to synchronize iPod"), e.Message);
            } finally {
                ReloadDatabase(true);
                FinishSave();
            }
        }
        
        private void CommitTrackToDevice(IpodDapTrackInfo track)
        {
            Song song = device.SongDatabase.CreateSong();
            
            song.Uri = track.Uri;
        
            if(track.Album != null) {
                song.Album = track.Album;
            }
            
            if(track.Artist != null) {
                song.Artist = track.Artist;
            }
            
            if(track.Title != null) {
                song.Title = track.Title;
            }
            
            if(track.Genre != null) {
                song.Genre = track.Genre;
            }
            
            song.Duration = track.Duration;
            song.TrackNumber = (int)track.TrackNumber;
            song.TotalTracks = (int)track.TrackCount;
            song.Year = (int)track.Year;
            song.LastPlayed = track.LastPlayed;
            
            switch(track.Rating) {
                case 1: song.Rating = SongRating.Zero; break;
                case 2: song.Rating = SongRating.Two; break;
                case 3: song.Rating = SongRating.Three; break;
                case 4: song.Rating = SongRating.Four; break;
                case 5: song.Rating = SongRating.Five; break;
                default: song.Rating = SongRating.Zero; break;
            }
            
            if(song.Artist == null) {
                song.Artist = String.Empty;
            }
            
            if(song.Album == null) {
                song.Album = String.Empty;
            }
            
            if(song.Title == null) {
                song.Title = String.Empty;
            }
            
            if(song.Genre == null) {
                song.Genre = String.Empty;
            }
        }
        
        public override Gdk.Pixbuf GetIcon(int size)
        {
            string prefix = "multimedia-player-";
            string id = null;

            switch(device.Model) {
                case DeviceModel.Color: id = "ipod-standard-color"; break;
                case DeviceModel.ColorU2: id = "ipod-U2-color"; break;
                case DeviceModel.Regular: id = "ipod-standard-monochrome"; break;
                case DeviceModel.RegularU2: id = "ipod-U2-monochrome"; break;
                case DeviceModel.Mini: id = "ipod-mini-silver"; break;
                case DeviceModel.MiniBlue: id = "ipod-mini-blue"; break;
                case DeviceModel.MiniPink: id = "ipod-mini-pink"; break;
                case DeviceModel.MiniGreen: id = "ipod-mini-green"; break;
                case DeviceModel.MiniGold: id = "ipod-mini-gold"; break;
                case DeviceModel.Shuffle: id = "ipod-shuffle"; break;
                case DeviceModel.NanoWhite: id = "ipod-nano-white"; break;
                case DeviceModel.NanoBlack: id = "ipod-nano-black"; break;
                case DeviceModel.VideoWhite: id = "ipod-video-white"; break;
                case DeviceModel.VideoBlack: id = "ipod-video-black"; break;
                default:
                    id = "ipod-standard-monochrome";
                    break;
            }
            
            Gdk.Pixbuf icon = IconThemeUtils.LoadIcon(prefix + id, size);
            
            if(icon != null) {
                return icon;
            }
            
            string path = ConfigureDefines.ICON_THEME_DIR 
                + String.Format("{0}x{0}", size)
                + Path.DirectorySeparatorChar
                + "devices" + 
                + Path.DirectorySeparatorChar
                + prefix + id + ".png";
                
            try {
                return new Gdk.Pixbuf(path);
            } catch(Exception) {
                return base.GetIcon(size);
            }
        }
        
        public override void SetName(string name)
        {
            device.Name = name;
            device.Save();
        }
        
        public override void SetOwner(string owner)
        {
            device.UserName = owner;
            device.Save();
        }
        
        public override string Name {
            get {
                return device.Name;
            }
        }
        
        public override string Owner {
            get {
                return device.UserName;
            }
        }
        
        public override ulong StorageCapacity {
            get {
                return device.VolumeSize;
            }
        }
        
        public override ulong StorageUsed {
            get {
                return device.VolumeUsed;
            }
        }
        
        public override bool IsReadOnly {
            get {
                return !device.CanWrite;
            }
        }
        
        public override bool IsPlaybackSupported {
            get {
                return true;
            }
        }
        
        public override string GenericName {
            get {
                return "iPod";
            }
        }
    }
}
