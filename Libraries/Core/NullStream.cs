using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Codeology.SharpCache
{

    internal class NullStream : Stream
    {

        private long position;
        private long length;

        public NullStream()
        {
            position = 0;
            length = 0;
        }

        #region Methods

        public override void Flush()
        {
            // Do nothing...
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0;
        }

        public override void SetLength(long value)
        {
            length = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            length += count;
        }

        #endregion

        #region Properties

        public override bool CanRead
        {
            get { 
                return false;
            }
        }

        public override bool CanSeek
        {
            get { 
                return false;
            }
        }

        public override bool CanWrite
        {
            get { 
                return true;
            }
        }

        public override long Length
        {
            get {
                return length;
            }
        }

        public override long Position
        {
            get {
                return position;
            }
            set {
                position = value;
            }
        }

        #endregion

    }

}
