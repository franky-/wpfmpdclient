﻿//    WpfMpdClient
//    Copyright (C) 2012, 2013 Paolo Iommarini
//    sakya_tg@yahoo.it
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.ComponentModel;

namespace WpfMpdClient
{
  public class ListboxEntry : INotifyPropertyChanged
  {
    public event PropertyChangedEventHandler PropertyChanged;
    EntryType m_Type;
    string m_Artist;
    string m_Album;
    Uri m_ImageUrl = null;

    public enum EntryType
    {
      Artist,
      Album
    }

    public EntryType Type
    {
      get { return m_Type; }
      set
      {
        m_Type = value;
        OnPropertyChanged("Type");
      }
    }

    public string Artist
    {
      get { return m_Artist; }
      set
      {
        m_Artist = value;
        OnPropertyChanged("Artist");
      }
    }

    public string Album
    {
      get { return m_Album; }
      set
      {
        m_Album = value;
        OnPropertyChanged("Album");
      }
    }

    public string Key
    {
      get
      {
        return string.Format("{0}_{1}_{2}", Type.ToString(), Artist, Album);
      }
    }

    public Uri ImageUrl
    {
      get { return m_ImageUrl; }
      set
      {
        m_ImageUrl = value;
        OnPropertyChanged("ImageUrl");
      }
    }

    protected void OnPropertyChanged(string name)
    {
      PropertyChangedEventHandler handler = PropertyChanged;
      if (handler != null) {
        handler(this, new PropertyChangedEventArgs(name));
      }
    }
  }

  public class ArtDownloader
  {
    bool m_Working = false;
    int m_Downloaders = 0;
    int m_MaxDownloaders = 5;
    List<ListboxEntry> m_Entries = new List<ListboxEntry>();
    Mutex m_Mutex = new Mutex();
    Mutex m_IndexMutex = new Mutex();

    Dictionary<string, Uri> m_Cache = new Dictionary<string, Uri>();

    public ArtDownloader()
    {
    }

    public void Start()
    {
      if (!m_Working)
        System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(Worker));
    }

    public void Stop()
    {
      m_Working = false;
    }

    public bool GetFromCache(ListboxEntry entry)
    {
      Uri uri = null;
      if (m_Cache.TryGetValue(entry.Key, out uri)) {
        entry.ImageUrl = uri;
        return true;
      }
      return false;
    }

    public void Add(ListboxEntry entry)
    {
      Add(entry, -1);
    }

    public void Add(ListboxEntry entry, int index)
    {
      if (GetFromCache(entry))
        return;

      m_Mutex.WaitOne();
      if (m_Entries.Contains(entry))
        m_Entries.Remove(entry);

      if (index < 0)
        m_Entries.Add(entry);
      else
        m_Entries.Insert(index, entry);
      m_Mutex.ReleaseMutex();
    }

    private void Worker(object state)
    {
      m_Working = true;
      while (m_Working) {
        if (m_Entries.Count > 0) {
          while (m_Downloaders < m_MaxDownloaders && m_Entries.Count > 0) {
            m_Mutex.WaitOne();
            ListboxEntry entry = m_Entries[0];
            m_Entries.RemoveAt(0);
            m_Mutex.ReleaseMutex();

            m_IndexMutex.WaitOne();
            m_Downloaders++;
            m_IndexMutex.ReleaseMutex();

            System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(Downloader), entry);
          }
          System.Threading.Thread.Sleep(50);
        } else
          System.Threading.Thread.Sleep(50);
      }
    }

    private void Downloader(object state)
    {
      ListboxEntry entry = state as ListboxEntry;
      try {
        Uri uri = null;
        if (m_Cache.TryGetValue(entry.Key, out uri))
          entry.ImageUrl = uri;
        else {
          string url = string.Empty;
          if (entry.Type == ListboxEntry.EntryType.Artist)
            url = LastfmScrobbler.GetArtistArt(entry.Artist, Scrobbler.ImageSize.medium);
          else
            url = LastfmScrobbler.GetAlbumArt(entry.Artist, entry.Album, Scrobbler.ImageSize.medium);
          if (!string.IsNullOrEmpty(url))
            entry.ImageUrl = new Uri(url);
          m_Cache[entry.Key] = entry.ImageUrl;
        }
      } catch (Exception){
      }finally {
        m_IndexMutex.WaitOne();
        m_Downloaders--;
        m_IndexMutex.ReleaseMutex();
      }
    }
  }
}
