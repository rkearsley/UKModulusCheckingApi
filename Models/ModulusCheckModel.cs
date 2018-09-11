using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel.DataAnnotations;

namespace UKModulusCheckingAPI.Models
{
    /// <summary>  
    ///  This class represents lines in the valacdos.txt file
    /// </summary>  
    public class Valacdos
    {
        public enum ModulusCheckMethod
        {
            None,
            MOD10,
            MOD11,
            DBLAL
        }
        readonly string SortStart;
        readonly string SortEnd;
        internal int SortStartInt { get; private set; }
        internal int SortEndInt { get; private set; }
        internal ModulusCheckMethod ModMethod { get; private set; }
        internal int[] Matrix { get; private set; }
        internal string Exception { get; private set; }

        /// <summary>  
        ///  Parses a Valacdos object from text file line
        /// </summary> 
        public Valacdos(string valacdosLine)
        {
            string[] lineSplit = valacdosLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (lineSplit.Length < 17)
            {
                throw new Exception("Invalid valacdos line");
            }
            this.SortStart = lineSplit[0];
            if (!Int32.TryParse(SortStart, out int tmpStart)) { throw new Exception("Invalid SortStart"); }
            SortStartInt = tmpStart;
            this.SortEnd = lineSplit[1];
            if (!Int32.TryParse(SortEnd, out int tmpEnd)) { throw new Exception("Invalid SortEnd"); }
            this.SortEndInt = tmpEnd;
            switch (lineSplit[2])
            {
                case "MOD10":
                    ModMethod = ModulusCheckMethod.MOD10;
                    break;
                case "MOD11":
                    ModMethod = ModulusCheckMethod.MOD11;
                    break;
                case "DBLAL":
                    ModMethod = ModulusCheckMethod.DBLAL;
                    break;
            }
            this.Matrix = new int[14];
            for (int i = 0; i < 14; i++)
            {
                if (!Int32.TryParse(lineSplit[i + 3], out Matrix[i]))
                {
                    throw new Exception("Invalid number in matrix");
                }
            }
            this.Exception = lineSplit.Length == 18 ? lineSplit[17] : null;
        }
    }

    /// <summary>  
    ///  This class holds the collection of valacdos and performs the modulus checks
    /// </summary>  
    public class ModulusChecker
    {
        static readonly int u = 0, v = 1, w = 2, x = 3, y = 4, z = 5, a = 6, b = 7, c = 8, d = 9, e = 10, f = 11, g = 12, h = 13;
        static List<Valacdos> modulusList = null;
        static void LoadTable()
        {
            modulusList = new List<Valacdos>();
            StreamReader streamReader = new StreamReader("valacdos.txt");
            string line = null;
            while ((line = streamReader.ReadLine()) != null)
            {
                Valacdos v = new Valacdos(line);
                modulusList.Add(v);
            }
        }

        public class ValidationRequest
        {
            [Required]
            [StringLength(6)]
            public string SortCode { get; set; }
            [Required]
            [StringLength(8)]
            public string AccountNumber { get; set; }
        }
        public class ValidationResult
        {
            public ValidationRequest ValidationRequest { get; set; }
            public ModulusResult[] ModulusResults { get; set; }
        }
        public class ModulusResult
        {
            public bool Pass { get; set; }
            public string Method { get; set; }
            public string Exception { get; set; }
            public bool ExceptionChecked { get; set; }
        }

        /// <summary>  
        ///  Validates the given Sort Number and Account Number against the Vocalink specification
        /// </summary> 
        public static ValidationResult Validate(ValidationRequest req)
        {
            if (modulusList == null) { LoadTable(); }
            if (req.SortCode.Length != 6) { throw new Exception("SortCode length is not 6"); }
            if (req.AccountNumber.Length != 8) { throw new Exception("AccountNumber length is not 8"); }
            if (!Int32.TryParse(req.SortCode, out int sortCodeInt)) { throw new Exception("SortCode is not a number"); }
            if (!Int32.TryParse(req.AccountNumber, out _)) { throw new Exception("AccountNumber is not a number"); }

            // find matching valacdos
            List<Valacdos> valacdosList = modulusList.Where(x => sortCodeInt >= x.SortStartInt && sortCodeInt <= x.SortEndInt).ToList();
            if (valacdosList.Count == 0) { throw new Exception("SortCode not found"); }

            string combinedSortAccount = req.SortCode + req.AccountNumber;

            ValidationResult res = new ValidationResult();
            res.ValidationRequest = req;
            List<ModulusResult> modResults = new List<ModulusResult>();

            foreach (Valacdos v in valacdosList)
            {
                ModulusResult vr = new ModulusResult() { Method = v.ModMethod.ToString(), Exception = v.Exception };

                // parse input digits to int
                int[] input = new int[14];
                for (int i = u; i <= h; i++)
                {
                    input[i] = Int32.Parse(combinedSortAccount[i].ToString());
                }

                int[] matrix = (int[])v.Matrix.Clone(); // value copy

                // global exception
                if (v.Exception != null)
                {
                    // Perform the check as specified, except if g = 9 zeroise weighting positions u-b
                    if ((v.Exception == "7") && (input[g] == 9))
                    {
                        for (int i = u; i <= b; i++)
                        {
                            matrix[i] = 0;
                        }
                        vr.ExceptionChecked = true;
                    }
                }

                // combine
                int[] output = new int[14];
                for (int i = u; i <= h; i++)
                {
                    output[i] = input[i] * matrix[i];
                }

                int sum = 0;
                if (v.ModMethod == Valacdos.ModulusCheckMethod.DBLAL)
                {
                    // Add all the numbers (individual digits) together
                    foreach (int i in output)
                    {
                        foreach (char c in i.ToString())
                        {
                            sum += Int32.Parse(c.ToString());
                        }
                    }
                }
                else
                {
                    // Add all the results (not individual digits) together
                    sum = output.Sum();
                }

                if (v.ModMethod == Valacdos.ModulusCheckMethod.MOD10)
                {
                    if ((sum % 10) == 0)
                    {
                        vr.Pass = true;
                    }
                }

                if (v.ModMethod == Valacdos.ModulusCheckMethod.MOD11)
                {
                    int mod = (sum % 11);
                    if (mod == 0)
                    {
                        vr.Pass = true;
                    }
                    if (v.Exception != null)
                    {
                        // ensure that the remainder is the same as the two-digit checkdigit; the checkdigit for
                        // exception 4 is gh from the original account number
                        if (v.Exception == "4")
                        {
                            string gh = input[g].ToString() + input[h].ToString();
                            if (mod == Int32.Parse(gh))
                            {
                                vr.Pass = true;
                            }
                            else
                            {
                                vr.Pass = false;
                            }
                            vr.ExceptionChecked = true;
                        }
                    }
                }

                if (v.ModMethod == Valacdos.ModulusCheckMethod.DBLAL)
                {
                    if ((sum % 10) == 0)
                    {
                        vr.Pass = true;
                    }
                }

                modResults.Add(vr);
            }

            // all checks finished
            res.ModulusResults = modResults.ToArray();
            return res;
        }
    }
}
