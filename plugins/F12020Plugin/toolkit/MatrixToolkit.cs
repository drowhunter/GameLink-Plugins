using System;
using System.Collections.Generic;
using System.Linq;

namespace TimHanewich.Toolkit
{
    public static class HanewichMatrixToolkit
    {
        public static string Print(this float[,] matrix)
        {
            //Get total # of rows and columns
            int ColCount = matrix.GetLength(1);
            int RowCount = matrix.GetLength(0);



            List<string> Rows = new List<string>();
            int r = 0;
            for (r = 0; r <= RowCount - 1; r++)
            {
                List<string> ThisRow = new List<string>();
                int c = 0;
                for (c = 0; c <= ColCount - 1; c++)
                {
                    ThisRow.Add(matrix[r, c].ToString());
                }

                string ThisRowStr = "";
                foreach (string s in ThisRow)
                {
                    ThisRowStr = ThisRowStr + s + "\t";
                }
                ThisRowStr = ThisRowStr.Substring(0, ThisRowStr.Length - 1);
                Rows.Add(ThisRowStr);
            }

            string ToReturn = "";
            foreach (string s in Rows)
            {
                ToReturn = ToReturn + s + Environment.NewLine;
            }
            ToReturn = ToReturn.Substring(0, ToReturn.Length - 1);

            return ToReturn;
        }

        public static float[,] MultiplyMatrices(float[,] matrix1, float[,] matrix2)
        {

            //Get total # of rows and columns
            int ColCount1 = matrix1.GetLength(1);
            int RowCount1 = matrix1.GetLength(0);
            int ColCount2 = matrix2.GetLength(1);
            int RowCount2 = matrix2.GetLength(0);

            if (ColCount1 != RowCount2)
            {
                throw new Exception("These matrices are not able to be multiplied due to their varying sizes.");
            }

            //Get size of our output matrix
            int OutputRowCount = RowCount1;
            int OutputColCount = ColCount2;
            float[,] ToReturn = new float[OutputRowCount, OutputColCount];



            //Fill in each row, then each column
            int r = 0;
            for (r = 0; r <= OutputRowCount - 1; r++)
            {
                int c = 0;
                for (c = 0; c <= OutputColCount - 1; c++)
                {
                    //Get column values
                    float[] cvals = GetColumnValues(matrix2, c);

                    //Get row values
                    float[] rvals = GetRowValues(matrix1, r);

                    float ThisDotProduct = HanewichMatrixToolkit.DotProduct(cvals, rvals);
                    ToReturn[r, c] = ThisDotProduct;
                }
            }

            return ToReturn;
        }

        public static float DotProduct(float[] arr1, float[] arr2)
        {
            if (arr1.Length != arr2.Length)
            {
                throw new Exception("Unable to calculate dot product of array with length " + arr1.Length.ToString() + " and array with length " + arr2.Length.ToString());
            }

            List<float> WeightedCalcs = new List<float>();
            int t = 0;
            for (t = 0; t <= arr1.Length - 1; t++)
            {
                WeightedCalcs.Add(arr1[t] * arr2[t]);
            }

            return WeightedCalcs.Sum();
        }

        public static float[] GetRowValues(this float[,] matrix, int row_index)
        {
            //Get row + col length
            int ColLength = matrix.GetLength(1);
            int RowLength = matrix.GetLength(0);

            if (row_index > (RowLength - 1))
            {
                throw new Exception("Unable to get row values because the subject matrix does not have enough rows as indicated.");
            }
            if (row_index < 0)
            {
                throw new Exception("Unable to get row values because the indicated row index was less than 0.");
            }


            List<float> Vals = new List<float>();

            int t = 0;
            for (t = 0; t <= ColLength - 1; t++)
            {
                Vals.Add(matrix[row_index, t]);
            }



            return Vals.ToArray();
        }

        public static float[] GetColumnValues(this float[,] matrix, int column_index)
        {
            //Get row + col length
            int ColLength = matrix.GetLength(1);
            int RowLength = matrix.GetLength(0);

            if (column_index > (ColLength - 1))
            {
                throw new Exception("Unable to get column values because the subject matrix does not have enough columns as indicated.");
            }
            if (column_index < 0)
            {
                throw new Exception("Unable to get column values because the indicated column index was less than 0.");
            }


            List<float> Vals = new List<float>();

            int t = 0;
            for (t = 0; t <= RowLength - 1; t++)
            {
                Vals.Add(matrix[t, column_index]);
            }



            return Vals.ToArray();
        }

        public static void RandomizeMatrixValues(this float[,] matrix)
        {
            Random rand = new Random();

            //Get row + col length
            int ColLength = matrix.GetLength(1);
            int RowLength = matrix.GetLength(0);

            int r = 0;
            int c = 0;
            for (r = 0; r <= RowLength - 1; r++)
            {
                for (c = 0; c <= ColLength - 1; c++)
                {
                    matrix[r, c] = (float)rand.NextDouble();
                }
            }
        }
    }
}