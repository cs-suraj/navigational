﻿
namespace GMap.NET.WindowsPresentation
{
   using System;
   using System.Collections.Generic;
   using System.ComponentModel;
   using System.Windows;
   using System.Windows.Input;
   using GMap.NET.Internals;
   using GMap.NET;

   /// <summary>
   /// form helping to prefetch tiles on local db
   /// </summary>
   public partial class TilePrefetcher : Window
   {
      BackgroundWorker worker = new BackgroundWorker();
      List<GMap.NET.Point> list = new List<GMap.NET.Point>();
      int zoom;
      MapType type;
      int sleep;
      int all;
      public bool ShowCompleteMessage = false;
      PureProjection prj;
      RectLatLng area;
      GMap.NET.Size maxOfTiles;

      public TilePrefetcher()
      {
         InitializeComponent();

         worker.WorkerReportsProgress = true;
         worker.WorkerSupportsCancellation = true;
         worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
         worker.DoWork += new DoWorkEventHandler(worker_DoWork);
         worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
      }

      public void Start(RectLatLng area, PureProjection prj, int zoom, MapType type, int sleep)
      {
         if(!worker.IsBusy)
         {
            this.label1.Content = "...";
            this.progressBar1.Value = 0;

            this.prj = prj;
            this.area = area;
            this.zoom = zoom;
            this.type = type;
            this.sleep = sleep;

            GMaps.Instance.UseMemoryCache = false;

            worker.RunWorkerAsync();

            this.ShowDialog();
         }
      }

      public void Stop()
      {
         if(worker.IsBusy)
         {
            worker.CancelAsync();
         }
      }

      void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
      {
         if(ShowCompleteMessage)
         {
            if(!e.Cancelled)
            {
               MessageBox.Show("Prefetch Complete! => " + ((int) e.Result).ToString() + " of " + all);
            }
            else
            {
               MessageBox.Show("Prefetch Canceled! => " + ((int) e.Result).ToString() + " of " + all);
            }
         }

         list.Clear();

         GMaps.Instance.UseMemoryCache = true;

         this.Close();
      }

      bool CacheTiles(ref MapType[] types, int zoom, GMap.NET.Point p)
      {
         foreach(MapType type in types)
         {
            Exception ex;
            PureImage img;

            // tile number inversion(BottomLeft -> TopLeft) for pergo maps
            if(type == MapType.PergoTurkeyMap)
            {
               img = GMaps.Instance.GetImageFrom(type, new GMap.NET.Point(p.X, maxOfTiles.Height - p.Y), zoom, out ex);
            }
            else // ok
            {
               img = GMaps.Instance.GetImageFrom(type, p, zoom, out ex);
            }

            if(img != null)
            {
               img.Dispose();
               img = null;
            }
            else
            {
               return false;
            }
         }
         return true;
      }

      void worker_DoWork(object sender, DoWorkEventArgs e)
      {
         if(list != null)
         {
            list.Clear();
            list = null;
         }
         list = prj.GetAreaTileList(area, zoom, 0);
         maxOfTiles = prj.GetTileMatrixMaxXY(zoom);
         all = list.Count;

         int countOk = 0;
         int retry = 0;

         Stuff.Shuffle<GMap.NET.Point>(list);
         var types = GMaps.Instance.GetAllLayersOfType(type);

         for(int i = 0; i < all; i++)
         {
            if(worker.CancellationPending)
               break;

            GMap.NET.Point p = list[i];
            {
               if(CacheTiles(ref types, zoom, p))
               {
                  countOk++;
                  retry = 0;
               }
               else
               {
                  if(++retry <= 1) // retry only one
                  {
                     i--;
                     System.Threading.Thread.Sleep(1111);
                     continue;
                  }
                  else
                  {
                     retry = 0;
                  }
               }
            }

            worker.ReportProgress((int) ((i+1)*100/all), i+1);

            System.Threading.Thread.Sleep(sleep);
         }

         e.Result = countOk;
      }

      void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
      {
         this.label1.Content = "Fetching tile at zoom (" + zoom + "): " + ((int) e.UserState).ToString() + " of " + all + ", complete: " + e.ProgressPercentage.ToString() + "%";
         this.progressBar1.Value = e.ProgressPercentage;
      }

      protected override void OnPreviewKeyDown(KeyEventArgs e)
      {
         if(e.Key == Key.Escape)
         {
            this.Close();
         }

         base.OnPreviewKeyDown(e);
      }

      protected override void OnClosed(EventArgs e)
      {
         this.Stop();

         base.OnClosed(e);
      }
   }
}
