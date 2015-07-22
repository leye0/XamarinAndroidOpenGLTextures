using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Graphics;
using Android.Media;
using Android.Opengl;

using Java.IO;
using Java.Nio;
using Javax.Microedition.Khronos.Egl;
using Javax.Microedition.Khronos.Opengles;
using Java.Lang;
using Android.Util;
using XamarinAndroidOpenGLTextures;
using System.IO;

namespace XamarinAndroidOpenGLTextures
{
	[Activity (Label = "XamarinAndroidOpenGLTextures", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{

		private GlVideoView _videoView;
		private string _workingDirectory;
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			SetContentView (Resource.Layout.Main);

			_videoView = FindViewById<GlVideoView>(Resource.Id.myVideoView);
			_workingDirectory = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
			var movie = System.IO.Path.Combine (_workingDirectory, "cat2.mp4");
			if (!System.IO.File.Exists(movie)) {
				CreateSampleFile(Resource.Raw.cat2, _workingDirectory, "cat2.mp4");
			}

			_videoView.SetSource (movie);

			FindViewById<Button>(Resource.Id.button1).Click += (sender, e) => 
			{
				_videoView.Play();
			};
		}

		private void CreateSampleFile(int resource, string destinationFolder, string filename) {
			var data = new byte[0];
			using (var file = Resources.OpenRawResource (resource))
			using (var fileInMemory = new MemoryStream ()) {
				file.CopyTo (fileInMemory);
				data = fileInMemory.ToArray ();
			}
			var fileName = System.IO.Path.Combine (destinationFolder, filename);
			System.IO.File.WriteAllBytes (fileName, data);
		}
	}
}


