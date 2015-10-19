using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Xna.Framework;

namespace SoftEngine.Renderer
{
    public class Camera
    {
        public Vector3 Position { get; set; } 
        public Vector3 Target { get; set; }
    }

    public struct Face
    {
        public int A;
        public int B;
        public int C;
    }

    public class Mesh
    {
        public string Name { get; set; }
        public Vector3[] Vertices { get; private set; }
        public Face[] Faces { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }

        public Mesh(string name, int verticesCount, int facesCount) {
            Vertices = new Vector3[verticesCount];
            Faces = new Face[facesCount];
            Name = name;
        }
    }

    public class Native
    {
        [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, int size);
    }

    public class Device
    {
        private byte[] _backBuffer;
        private WriteableBitmap _bitmap;

        public Device(WriteableBitmap bitmap) {
            _bitmap = bitmap;
            _backBuffer = new Byte[_bitmap.PixelWidth*_bitmap.PixelHeight*4];
        }

        public void Clear(byte r, byte g, byte b, byte a) {
            for (var index = 0; index < _backBuffer.Length; index += 4) {
                _backBuffer[index] = b;
                _backBuffer[index + 1] = g;
                _backBuffer[index + 2] = r;
                _backBuffer[index + 3] = a;
            }
        }

        public void Present() {
            _bitmap.Lock();
            unsafe {
                var ptrBackBuffer = (int) _bitmap.BackBuffer;

                byte[] data = _backBuffer;
                GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
                IntPtr ptrData = pinnedArray.AddrOfPinnedObject();

                Native.CopyMemory(_bitmap.BackBuffer, ptrData, _bitmap.PixelWidth*_bitmap.PixelHeight*4);
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
            _bitmap.Unlock();
        }

        public void PutPixel(int x, int y, Color color) {
            var idx = (x + y*_bitmap.PixelWidth)*4;

            _backBuffer[idx] = (byte) (color.B*255);
            _backBuffer[idx + 1] = (byte) (color.G*255);
            _backBuffer[idx + 2] = (byte) (color.R*255);
            _backBuffer[idx + 3] = (byte) (color.A*255);
        }

        public Vector2 Project(Vector3 coord, Matrix transMat) {
            var vec = Vector3.Divide(coord, 10.0f);
            var point = Vector3.Transform(vec, transMat);

            // And transform again to x:0, y:0 on the top-left

            var x = point.X*_bitmap.PixelWidth + _bitmap.PixelWidth/2.0f;
            var y = -point.Y*_bitmap.PixelHeight + _bitmap.PixelHeight/2.0f;

            return new Vector2(x, y);
        }

        public void DrawPoint(Vector2 point) {
            if (point.X >= 0 && point.Y >= 0 && point.X < _bitmap.PixelWidth && point.Y < _bitmap.PixelHeight) {
                PutPixel((int) point.X, (int) point.Y, new Color(1.0f, 1.0f, 0.0f, 1.0f));
            }
        }

        public void DrawLine(Vector2 point0, Vector2 point1) {
            var dist = (point1 - point0).Length();

            if (dist < 2)
                return;

            Vector2 middlePoint = point0 + (point1 - point0)/2;
            DrawPoint(middlePoint);

            // Fill in the points recursively.
            DrawLine(point0, middlePoint);
            DrawLine(middlePoint, point1);
        }

        public void Render(Camera camera, params Mesh[] meshes) {
            var viewMatrix = Matrix.CreateLookAt(camera.Position, camera.Target, Vector3.UnitY);
            var projMatrix = Matrix.CreatePerspectiveFieldOfView(0.90f, (float) _bitmap.PixelWidth/_bitmap.PixelHeight, 0.01f, 1.0f);

            foreach (var mesh in meshes) {
                // Rotate /then/ Translate
                var worldMatrix = Matrix.CreateFromYawPitchRoll(mesh.Rotation.Y, mesh.Rotation.X, mesh.Rotation.Z)*
                                  Matrix.CreateTranslation(mesh.Position);

                var transformMatrix = worldMatrix*viewMatrix*projMatrix;

                foreach (var face in mesh.Faces) {
                    var vertA = mesh.Vertices[face.A];
                    var vertB = mesh.Vertices[face.B];
                    var vertC = mesh.Vertices[face.C];

                    var pixelA = Project(vertA, transformMatrix);
                    var pixelB = Project(vertB, transformMatrix);
                    var pixelC = Project(vertC, transformMatrix);

                    DrawLine(pixelA, pixelB);
                    DrawLine(pixelB, pixelC);
                    DrawLine(pixelC, pixelA);
                }
            }
        }
    }
}
