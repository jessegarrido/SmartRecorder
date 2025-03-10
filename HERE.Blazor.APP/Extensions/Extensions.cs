﻿using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HERE
{
    internal static class Extensions
    {
        public static string? TranslateString(this string untranslatedstring)
        {
            try
            {
                var translatedstring = untranslatedstring.Replace("[Date]", DateOnly.FromDateTime(DateTime.Now).ToString(("yyyy-MM-dd")), StringComparison.CurrentCultureIgnoreCase);
                translatedstring = translatedstring.Replace("[#]", "X");
                return translatedstring;
            }
            catch (Exception)
            {
                return untranslatedstring;
            }
        }

	}
}
