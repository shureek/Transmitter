using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using KSoft;

namespace UnitTest
{
    [TestClass]
    public class TestClass
    {
        [TestMethod]
        public void IList_FirstOccurenceOf()
        {
            byte[] array = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            Assert.AreEqual(array.FirstOccurenceOf(new byte[] {5, 6, 7}), 5);
            Assert.AreEqual(array.FirstOccurenceOf(new byte[] {5, 6}), 5);
            Assert.AreEqual(array.FirstOccurenceOf(new byte[] {9, 10}), 9);
            Assert.AreEqual(array.FirstOccurenceOf(new byte[] {6}), 6);
            Assert.AreEqual(array.FirstOccurenceOf(new byte[] {10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 1, 1, 1, 1}), null);
        }

        [TestMethod]
        public void ArraySegment_Enumerator()
        {
            byte[] array = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int offset = 3;
            int length = 5;
            int index = offset;
            var segment = new KSoft.ArraySegment<byte>(array, offset, length);
            foreach (var b in segment)
            {
                Assert.AreEqual(b, array[index]);
                index++;
            }
        }
    }
}
