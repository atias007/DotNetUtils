using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoAzureFunc.Utils
{
    internal class HtmlFormatter
    {
        private const string template = """
            <head>
              <style>
                .last-update {
            	  font-family: Consolas, Tahoma, Geneva, sans-serif;
                  font-size: 12px;
                  padding: 4px;
            	  margin: 4px;
            	}
                table {
                  border-collapse: collapse;
                  font-family: Consolas, Tahoma, Geneva, sans-serif;
                }
                table td {
                  padding: 10px;
                }
                table thead td {
                  padding: 8px;
                  background-color: #54585d;
                  color: #ffffff;
                  font-weight: bold;
                  font-size: 14px;
                  border: 1px solid #54585d;
                }
                table tbody td {
                  color: #636363;
                  font-size: 14px;
                  border: 1px solid #dddfe1;
                }
                table tbody tr {
                  background-color: #f9fafb;
                }
                table tbody tr:nth-child(odd) {
                  background-color: #ffffff;
                }
              </style>
            <head>
            <body>
              <div class="last-update">Last update: @@LastUpdate@@</div>
              <table>
                <thead>
                  <!-- @@Header@@ -->
                </thead>
                <tbody>
                  <!-- @@Body@@ -->
                </tbody>
              </table>
            </body>
            """;

        public static string Format(string lastUpdate, string headers, IEnumerable<string> rows)
        {
            var sb = new StringBuilder(template);
            sb.Replace("@@LastUpdate@@", lastUpdate);
            sb.Replace("<!-- @@Header@@ -->", headers);
            sb.Replace("<!-- @@Body@@ -->", string.Join(Environment.NewLine, rows));
            return sb.ToString();
        }
    }
}