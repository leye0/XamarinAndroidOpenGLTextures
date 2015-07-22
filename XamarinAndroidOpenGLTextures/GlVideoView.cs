using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Opengl;
using Android.Util;
using Android.Views;
using Java.IO;
using Java.Lang;
using Java.Nio;

namespace XamarinAndroidOpenGLTextures
{
	public class GlVideoView : GLSurfaceView {
		private VideoPreviewer _previewer;
		private MediaPlayer _mediaPlayer = null;
		private Context _context;

		public GlVideoView(Context context, IAttributeSet attrs) : base(context, attrs) { 
			_context = context;
			Init ();
		}

		public GlVideoView(Context context) : base(context, null) {
			_context = context;
			Init ();
		}

		private void Init() {
			SetEGLContextClientVersion (2);
			Holder.SetFormat (Android.Graphics.Format.Translucent);
			Holder.SetFormat (Android.Graphics.Format.Opaque);
			SetEGLConfigChooser (8, 8, 8, 8, 16, 0);
			_previewer = new VideoPreviewer (_context, this);
			SetRenderer (_previewer);
			RenderMode = Rendermode.WhenDirty;

		}

		public void SetSource(string movie) {
			_previewer.SetSource (movie);	
		}


		public void Stop() {
			_previewer.Stop ();
		}

		public void Play() {
			_previewer.Play ();
		}

		public void Pause() {
			_previewer.Pause ();
		}

		public void SeekTo(float percent) {
			_previewer.SeekTo (percent);
		}

		public override void OnResume() {
			base.OnResume ();
		}

		public override void OnPause() {
			base.OnPause();
		}

		protected override void OnDetachedFromWindow() {

			base.OnDetachedFromWindow();

			if (_mediaPlayer != null) {
				_mediaPlayer.Stop();
				_mediaPlayer.Release();
				_previewer.Dispose ();
			}
		}

		private class VideoPreviewer : Java.Lang.Object, GLSurfaceView.IRenderer {

			private const int FLOAT_SIZE_BYTES = 4;
			private const int TRIANGLE_VERTICES_DATA_STRIDE_BYTES = 3 * FLOAT_SIZE_BYTES;
			private const int TEXTURE_VERTICES_DATA_STRIDE_BYTES = 2 * FLOAT_SIZE_BYTES;
			private const int TRIANGLE_VERTICES_DATA_POS_OFFSET = 0;
			private const int TRIANGLE_VERTICES_DATA_UV_OFFSET = 0;

			private float[] _triangleVerticesData = { -1.0f, -1.0f, 0, 1.0f,
				-1.0f, 0, -1.0f, 1.0f, 0, 1.0f, 1.0f, 0, };


			private float[] _textureVerticesData = { 
				0.0f, 0.0f, 
				1.0f, 0.0f,
				0.0f, 1.0f, 
				1.0f, 1.0f
			};

			private FloatBuffer _triangleVertices;

			private FloatBuffer _textureVertices;

			private readonly object syncLock = new object();

			private float[] _MVPMatrix = new float[16];
			private float[] _STMatrix = new float[16];
			private float[] _projectionMatrix = new float[16];

			private int _glProgram;
			private int _OESTextureId;
			private int _uMVPMatrixHandle;
			private int _uSTMatrixHandle;
			private int _aPositionHandle;
			private int _aTextureCoord;

			private SurfaceTexture _surfaceTexture;
			private bool _updateSurface = false;
			private MediaPlayer _mediaPlayer;
			private string _filepath;
			private int GL_TEXTURE_EXTERNAL_OES = 0x8D65;
			private GlVideoView _videoView;

			public VideoPreviewer(Context context, GlVideoView videoView) {
				_mediaPlayer = new MediaPlayer ();
				_videoView = videoView;
				_triangleVertices = ByteBuffer.AllocateDirect(_triangleVerticesData.Length * FLOAT_SIZE_BYTES)
					.Order(ByteOrder.NativeOrder()).AsFloatBuffer();

				_triangleVertices.Put(_triangleVerticesData).Position(0);

				_textureVertices = ByteBuffer.AllocateDirect(_textureVerticesData.Length * FLOAT_SIZE_BYTES)
					.Order(ByteOrder.NativeOrder()).AsFloatBuffer();

				_textureVertices.Put(_textureVerticesData).Position(0);

				Android.Opengl.Matrix.SetIdentityM(_STMatrix, 0);
			}


			private int _otherTextureId;

			private int _otherTextureUniform;
			private ByteBuffer funnyGhostEffectBuffer;
			public void OnDrawFrame(Javax.Microedition.Khronos.Opengles.IGL10 glUnused) {

				if (_updateSurface) {
					_surfaceTexture.UpdateTexImage ();
					_surfaceTexture.GetTransformMatrix (_STMatrix);
					_updateSurface = false;
				}

				GLES20.GlUseProgram (0);
				GLES20.GlUseProgram (_glProgram);
				GLES20.GlActiveTexture (GLES20.GlTexture2);
				var tWidth = _width;
				var tHeight = _height;

				funnyGhostEffectBuffer = ByteBuffer.AllocateDirect (tWidth * tHeight * 4);
				funnyGhostEffectBuffer.Order(ByteOrder.NativeOrder());
				funnyGhostEffectBuffer.Position(0);

				// Note that it is read in GlReadPixels in a different pixel order than top-left to lower-right, so it adds a reversed+mirror effect
				// when passed to TexImage2D to convert to texture.
				GLES20.GlReadPixels (0, 0, tWidth - 1, tHeight - 1, GLES20.GlRgba, GLES20.GlUnsignedByte, funnyGhostEffectBuffer);
				updateTargetTexture (tWidth, tHeight);
				GLES20.GlBindTexture(GLES20.GlTexture2d, _otherTextureId);
				GLES20.GlUniform1i (_otherTextureUniform, 2);

				GLES20.GlUseProgram (0);
				GLES20.GlUseProgram (_glProgram);
				GLES20.GlActiveTexture (GLES20.GlTexture1);
				GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, _OESTextureId);
				GLES20.GlUniform1i (_OESTextureUniform, 1);

				_triangleVertices.Position (TRIANGLE_VERTICES_DATA_POS_OFFSET);
				GLES20.GlVertexAttribPointer (_aPositionHandle, 3, GLES20.GlFloat, false, TRIANGLE_VERTICES_DATA_STRIDE_BYTES, _triangleVertices);
				GLES20.GlEnableVertexAttribArray (_aPositionHandle);

				_textureVertices.Position (TRIANGLE_VERTICES_DATA_UV_OFFSET);
				GLES20.GlVertexAttribPointer (_aTextureCoord, 2, GLES20.GlFloat, false, TEXTURE_VERTICES_DATA_STRIDE_BYTES, _textureVertices);
				GLES20.GlEnableVertexAttribArray (_aTextureCoord);

				Android.Opengl.Matrix.SetIdentityM (_MVPMatrix, 0);
				GLES20.GlUniformMatrix4fv (_uMVPMatrixHandle, 1, false, _MVPMatrix, 0);
				GLES20.GlUniformMatrix4fv (_uSTMatrixHandle, 1, false, _STMatrix, 0);

				GLES20.GlDrawArrays (GLES20.GlTriangleStrip, 0, 4);

				GLES20.GlFinish ();

			}

			private int CreateTargetTexture() {
				
				int texture;
				int[] textures = new int[1];
				GLES20.GlGenTextures(1, textures, 0);
				texture = textures[0];
				updateTargetTexture (32, 32);
				return texture;
			}

			private byte[] _buffer;

			private void updateTargetTexture(int width, int height) {
				GLES20.GlActiveTexture(GLES20.GlTexture2);
				_buffer = new byte[0];

				if (funnyGhostEffectBuffer == null) {
					_buffer = new byte[4 * width * height];
					for (var i = 0; i < (width * height * 4); i++) {
						_buffer [i] = (byte)((System.Math.Sin (i) * 127) + 127);
					}
				}

				GLES20.GlBindTexture(GLES20.GlTexture2d, _otherTextureId);
				GLES20.GlUniform1i(_otherTextureUniform, 2);
				GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureMinFilter,
					GLES20.GlNearest);
				GLES20.GlTexParameterf(GLES20.GlTexture2d, GLES20.GlTextureMagFilter,
					GLES20.GlLinear);
				GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapS, 
					GLES20.GlClampToEdge);
				GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapT, 
					GLES20.GlClampToEdge);
				
				GLES10.GlTexImage2D (GLES20.GlTexture2d, 0, GLES20.GlRgba, width, height, 0, GLES20.GlRgba, GLES20.GlUnsignedByte, funnyGhostEffectBuffer ?? ByteBuffer.Wrap (_buffer));
				if (funnyGhostEffectBuffer != null) {
					funnyGhostEffectBuffer.Clear ();
					funnyGhostEffectBuffer.Dispose ();
				}

			}

			protected override void Dispose (bool disposing)
			{
				base.Dispose (disposing);
				GLES20.GlDeleteTextures(2, new int[]{ _OESTextureId, _otherTextureId }, 0);
			}

			int _width;
			int _height;
			private float[] projectionMatrix = new float[16];
			public void OnSurfaceChanged(Javax.Microedition.Khronos.Opengles.IGL10 glUnused, int width, int height) {
				_width = width; _height = height;
				GLES20.GlViewport (0, 0, width, height);

				Android.Opengl.Matrix.FrustumM (projectionMatrix, 0, -1.0f, 1.0f, -1.0f, 1.0f,
					1.0f, 10.0f);
			}

			private const string mVertexShader =
				"uniform mat4 uMVPMatrix;\n" +
				"uniform mat4 uSTMatrix;\n" +
				"attribute vec4 aPosition;\n" +
				"attribute vec4 aTextureCoord;\n" +
				"varying vec2 vTextureCoord;\n" +
				"void main() {\n" +
				"  gl_Position = uMVPMatrix * aPosition;\n" +
				"  vTextureCoord = (uSTMatrix * aTextureCoord).xy;\n" +
				"}\n";

			private static string textureName = "zzzTexture";

			private string mFragmentShader =
				"#extension GL_OES_EGL_image_external : require\n" +
				"precision mediump float;\n" +
				"varying vec2 vTextureCoord;\n" +
				string.Format("uniform samplerExternalOES {0};\n", textureName) +
				"uniform sampler2D overlay;\n" +
				"void main() {\n" +
				string.Format("  lowp vec4 color1 = texture2D({0}, vTextureCoord);\n", textureName) +	
				"  lowp vec4 color3 = texture2D(overlay, vTextureCoord);\n" +
				"  gl_FragColor = mix(color1, color3, 0.5);\n" +
				"}\n";

			public void OnSurfaceCreated(Javax.Microedition.Khronos.Opengles.IGL10 gl, Javax.Microedition.Khronos.Egl.EGLConfig config) {

				_glProgram = CreateProgram(mVertexShader, mFragmentShader);

				var extensions = " " + GLES20.GlGetString(GLES20.GlExtensions) + " ";
				var ok = extensions.IndexOf("GL_OES_framebuffer_object") >= 0;
				var ab = extensions;

				if (_glProgram == 0) {
					return;
				}

				GLES20.GlUseProgram(0);
				GLES20.GlUseProgram (_glProgram);

				_aPositionHandle = ActOnError (GLES20.GlGetAttribLocation (_glProgram, "aPosition"), "aPosition");
				_aTextureCoord = ActOnError(GLES20.GlGetAttribLocation(_glProgram, "aTextureCoord"), "aTextureCoord");
				_uMVPMatrixHandle = ActOnError(GLES20.GlGetUniformLocation(_glProgram, "uMVPMatrix"), "uMVPMatrix");
				_uSTMatrixHandle = ActOnError(GLES20.GlGetUniformLocation(_glProgram, "uSTMatrix"), "uSTMatrix");
				_OESTextureUniform = ActOnError(GLES20.GlGetUniformLocation(_glProgram, textureName), textureName);
				_otherTextureUniform = ActOnError(GLES20.GlGetUniformLocation(_glProgram, "overlay"), "overlay");

				int[] textures = new int[1];
				GLES20.GlGenTextures(1, textures, 0);

				_OESTextureId = textures[0];
				GLES20.GlUseProgram (0);
				GLES20.GlUseProgram (_glProgram);
				GLES20.GlActiveTexture(GLES20.GlTexture1);
				GLES20.GlBindTexture(GL_TEXTURE_EXTERNAL_OES, _OESTextureId);
				GLES20.GlUniform1i (_OESTextureUniform, 1);
				GLES20.GlUseProgram (0);
				GLES20.GlUseProgram (_glProgram);

				GLES20.GlTexParameterf(GL_TEXTURE_EXTERNAL_OES, GLES20.GlTextureMinFilter,
					GLES20.GlNearest);
				GLES20.GlTexParameterf(GL_TEXTURE_EXTERNAL_OES, GLES20.GlTextureMagFilter,
					GLES20.GlLinear);

				_surfaceTexture = new SurfaceTexture(_OESTextureId);
				_surfaceTexture.FrameAvailable += _surfaceTexture_FrameAvailable;
				Surface surface = new Surface(_surfaceTexture);
				_mediaPlayer.SetSurface(surface);
				surface.Release();

				_otherTextureId = CreateTargetTexture ();
				GLES20.GlActiveTexture(GLES20.GlTexture2);
				GLES20.GlBindTexture(GLES20.GlTexture2d, _otherTextureId);
				GLES20.GlUniform1i (_otherTextureUniform, 2);

				_updateSurface = false;
				_mediaPlayer.Start();
			}

			int _OESTextureUniform;

			void _surfaceTexture_FrameAvailable (object sender, SurfaceTexture.FrameAvailableEventArgs e)
			{
				onFrameAvailable(e.SurfaceTexture);
			}

			private Surface _texture;

			// A public method to switch current movie
			public void SetSource(string filepath) {
				_filepath = filepath;

				if (filepath != _filepath) {
					Reset ();
				}
			}

			public void Stop(bool rewind = true) {
				if (_texture != null) {

					if (rewind) {
						_mediaPlayer.SeekTo (0);
					}
					_mediaPlayer.Stop ();

				}
			}

			public void Reset(bool thenPlay = false) {
				Stop (false);
				if (_texture != null) {
					_mediaPlayer.Reset ();
				}

				InitMediaPlayerWithSurface (thenPlay);
			}

			public void InitMediaPlayerWithSurface(bool thenPlay = false) {

				var t = GLES20.GlGetString (GLES20.GlExtensions);
				//System.Diagnostics.Debug.WriteLine (t);
				// 1. Dispose and create
				_mediaPlayer.Dispose ();
				_mediaPlayer = new MediaPlayer ();

				// 2. Set surface
				_texture = new Surface(_surfaceTexture);
				_mediaPlayer.SetSurface (_texture);
				_texture.Release ();

				// 3. Set data source
				_mediaPlayer.SetDataSource (_filepath);

				// 4. Prepare (with surface)
				try {
					_mediaPlayer.Prepared += (object mediaPlayerPrepared, EventArgs mediaPlayerPreparedEvents) => {

						// 5. Should not update surface yet	
						lock (syncLock) {
							_updateSurface = false;
						}

						// 6. Set the three event handlers

						_mediaPlayer.Error += (sender, e) =>  {
							System.Diagnostics.Debug.WriteLine("Error");
						};

						_mediaPlayer.Info += (sender, e) =>  {
							System.Diagnostics.Debug.WriteLine("Info");
						};

						_mediaPlayer.Completion += (object sender, EventArgs e) => {
							System.Diagnostics.Debug.WriteLine("Completed");
						};

						if (thenPlay) {
							_mediaPlayer.Start();	
						}
					};
					_mediaPlayer.Prepare ();	
				} catch (System.Exception) {
					// Probably incompatible movie
					return;
				}
			}

			// Play movie - Always need to check that the player is ready, otherwise pass through some Reset procedure
			public void Play() {
				if (_isPaused) {
					_isPaused = false;
					_mediaPlayer.Start ();
					return;
				}

				Reset (true);
			}

			private bool _isPaused = false;
			public void Pause() {
				_mediaPlayer.Pause ();
				_isPaused = true;
			}

			public void SeekTo(float percent) {
				_mediaPlayer.SeekTo ((int)(percent * _mediaPlayer.Duration));
			}

			// Tells our video view that surface texture has a new image to show
			public void onFrameAvailable(SurfaceTexture surface) {

				lock (syncLock) {
					_updateSurface = true;
				}

				_videoView.RequestRender ();
			}

			// Used to eventually trigger a fail-safe mode when an attrib / uniform is missing
			int ActOnError(int result, string reason) {
				if (result == -1) {
					throw new System.Exception (reason);
				}
				return result;
			}

			private int LoadShader(int shaderType, string source) {
				int shader = GLES20.GlCreateShader(shaderType);
				if (shader != 0) {
					GLES20.GlShaderSource(shader, source);
					GLES20.GlCompileShader(shader);
					int[] compiled = new int[1];
					GLES20.GlGetShaderiv(shader, GLES20.GlCompileStatus,
						compiled, 0);
					if (compiled[0] == 0) {
						GLES20.GlDeleteShader(shader);
						shader = 0;
					}
				}
				return shader;
			}

			private int CreateProgram(string vertexSource, string fragmentSource) {

				var vertexShader = LoadShader(GLES20.GlVertexShader, vertexSource);

				if (vertexShader == 0) {
					return 0;
				}

				var fragmentShader = LoadShader(GLES20.GlFragmentShader, fragmentSource);

				if (fragmentShader == 0) {
					return 0;
				}

				int program = GLES20.GlCreateProgram();

				if (program != 0) {
					int[] shaderStatus = new int[1];

					GLES20.GlAttachShader(program, vertexShader);
					program = DefaultProgramIfFail("glAttachShader", program);
					if (program == 0) {
						return 0;
					}
						
					GLES20.GlAttachShader(program, fragmentShader);
					program = DefaultProgramIfFail("glAttachShader", program);
					if (program == 0) {
						return 0;
					}
						
					GLES20.GlLinkProgram(program);
					int[] linkStatus = new int[1];

					GLES20.GlGetProgramiv(program, GLES20.GlLinkStatus,
						linkStatus, 0);
					
					if (linkStatus[0] != GLES20.GlTrue) {
						var error = "Could not link program: " + GLES20.GlGetProgramInfoLog (program);
						GLES20.GlDeleteProgram(program);
						return 0;
					}
				}
				return program;
			}

			// A small check on current GLES context for consistency error - Means something is misused.
			private int DefaultProgramIfFail(string op, int programErr = -1) {
				int error;
				while ((error = GLES20.GlGetError()) != GLES20.GlNoError) {
					return 0;
				}
				return programErr;
			}
		}
	}
}

