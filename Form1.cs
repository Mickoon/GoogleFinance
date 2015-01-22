using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;

namespace WebCrawler
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        /* Google Finance News */
        private void button1_Click(object sender, EventArgs e)
        {
            var db = new FinanceCrawlerEntities();
            int duplicates = 0;
            int newData = 0;

            // Retreive source code from a webpage
            string url = textBox1.Text;
            if (url != null && url.Trim() != "")
            {
                try
                {
                    string sourceCode = WorkerClasses.getSourceCode(url);
                    if (sourceCode == "invalid") throw new UriFormatException();

                    /* Group */
                    string groupWord = textBox2.Text;
                    if (groupWord == "")
                        groupWord = WorkerClasses.getGroupWord(url);

                    #region GOOGLE FINANCE NEWS RESULT ONLY ALLOWED
                    while (sourceCode.IndexOf("g-section news") > -1)
                    {
                        try
                        {
                            int startIndex = sourceCode.IndexOf("g-section news");  // News article's information starts from here
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            StreamWriter sw = new StreamWriter("website.txt");
                            sw.Write(sourceCode);
                            sw.Close();

                            /* Article's Page Link */
                            startIndex = sourceCode.IndexOf("span class=name");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("a href=");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("\"") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("\"");
                            string pageLink = sourceCode.Substring(0, endIndex);
                            pageLink = pageLink.Replace("%3F", "?");
                            pageLink = pageLink.Replace("%3D", "=");
                            pageLink = pageLink.Replace("%26", "&");
                            if (pageLink.Contains("url="))
                            {
                                startIndex = pageLink.IndexOf("url=") + 4;
                                pageLink = pageLink.Substring(startIndex, pageLink.Length - startIndex);
                                if (pageLink.Contains("&amp;"))
                                {
                                    endIndex = pageLink.IndexOf("&amp;");
                                    pageLink = pageLink.Substring(0, endIndex);
                                }
                            }
                            //MessageBox.Show("Article Link is: " + pageLink);

                            /* Ariticle's Title */
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</a>");
                            string pageTitle = sourceCode.Substring(0, endIndex);
                            pageTitle = pageTitle.Replace("&nbsp;", " ");
                            pageTitle = pageTitle.Replace("&#39;", "'");
                            pageTitle = pageTitle.Replace("&quot;", "\"");
                            pageTitle = pageTitle.Replace("&amp;", "&");
                            //MessageBox.Show("Article Title is: " + pageTitle);

                            /* Ariticle's source website name */
                            startIndex = sourceCode.IndexOf("<span class=src") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</span>");
                            string webpage = sourceCode.Substring(0, endIndex);
                            //MessageBox.Show("Article source webpage is: " + webpage);

                            /* Ariticle's created date */
                            startIndex = sourceCode.IndexOf("<span class=date") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</span>");
                            string createdDateStr = sourceCode.Substring(0, endIndex);
                            DateTime createdDate = DateTime.Now;
                            if (!createdDateStr.Contains("ago"))
                                createdDate = Convert.ToDateTime(createdDateStr);
                            else
                            {
                                if (createdDateStr.Contains("hour"))
                                {
                                    createdDateStr = createdDateStr.Replace(" hours ago", "");
                                    createdDateStr = createdDateStr.Replace(" hour ago", "");
                                    createdDate = DateTime.Now.AddHours(-Int32.Parse(createdDateStr));
                                }
                                else if (createdDateStr.Contains("minute"))
                                {
                                    createdDateStr = createdDateStr.Replace(" minutes ago", "");
                                    createdDateStr = createdDateStr.Replace(" minute ago", "");
                                    createdDate = DateTime.Now.AddMinutes(-Int32.Parse(createdDateStr));
                                }
                            }

                            if (!db.GoogleFinance_News.Any(f => f.Link == pageLink))
                            {
                                db.GoogleFinance_News.Add(new GoogleFinance_News
                                {
                                    Group = groupWord,
                                    Title = pageTitle,
                                    Website = webpage,
                                    Date = createdDate,
                                    Link = pageLink
                                });
                                db.SaveChanges();
                                newData++;
                            }
                            else
                            {
                                duplicates++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add("Failed because of an error: " + ex);
                            continue;
                        }
                    }

                    errorBox.Items.Add("\n[" + DateTime.Now + "] Task Ended.\n" + newData + " saved and " + duplicates + " duplicates Found.");
                    #endregion
                }
                catch (Exception)
                {
                    errorBox.Items.Add("Invalid URL!");
                    MessageBox.Show("Invalid URL!");
                    textBox1.Text = "";
                }
            }
            else
            {
                errorBox.Items.Add("Please enter URL.");
                MessageBox.Show("Please enter URL.");
            }
        }

        private void exitButton_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        /* Articles */
        private void button2_Click(object sender, EventArgs e)
        {
            var db = new FinanceCrawlerEntities();
            int saved = 0;
            int duplicates = 0;
            int updatedData = 0;
            int wordsDuplicates = 0;
            var articlesList = (from f in db.GoogleFinance_News
                                orderby f.Website
                                select f).ToList();

            // Positive Words
            string positiveWordLink = "http://www3.nd.edu/~mcdonald/Data/Finance_Word_Lists/LoughranMcDonald_Positive.csv";
            string positiveCode = WorkerClasses.getSourceCode(positiveWordLink).ToLower().Replace("\n", "");
            if (positiveCode == "invalid") throw new UriFormatException();
            string positiveCodeCopy = positiveCode;

            // Negative Words
            string negativeWordLink = "http://www3.nd.edu/~mcdonald/Data/Finance_Word_Lists/LoughranMcDonald_Negative.csv";
            string negativeCode = WorkerClasses.getSourceCode(negativeWordLink).ToLower().Replace("\n", "");
            if (negativeCode == "invalid") throw new UriFormatException();
            string negativeCodeCopy = negativeCode;

            errorBox.Items.Add("Web scraping starts! Total " + articlesList.Count + " exists in database.");
            errorBox.Items.Add("Please be patient and wait for a few minutes.");
            errorBox.Items.Add("---------------------------------------------------------");

            foreach (var u in articlesList)
            {
                if (u.Story == null || u.Story == "")
                {
                    #region 123Jump.com
                    if (u.Website.Trim().ToLower().Contains("123jump.com"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("Author:");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(":") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("<tr valign=\"middle\">");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("<table>");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</td>");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</table>");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region 3BL Media (press release)
                    if (u.Website.Trim().ToLower().Contains("3bl media"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("id=\"fmr-title\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("id=\"fmr-body\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("id=\"fmr-resources\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region ABC Online
                    if (u.Website.Trim().ToLower() == ("abc online"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#039;", "'");

                            /* Author */
                            string author = "Undefined";
                            if (sourceCode.Contains("div class=\"byline\""))
                            {
                                startIndex = sourceCode.IndexOf("div class=\"byline\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("<span>");
                                if (sourceCode.IndexOf("div class=\"byline\"") + 200 < startIndex)
                                    startIndex = sourceCode.IndexOf("<a");
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("</");
                                    author = sourceCode.Substring(0, endIndex).Trim();
                                }
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("story-map");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"first\"");
                            if (startIndex == -1) startIndex = 0;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("class=\"topics");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"published");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region ABN Newswire (press release)
                    if (u.Website.Trim().ToLower().Contains("abn newswire"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("class=\"article-title\"");
                            int endIndex = 0;
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("<label");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                            }
                            else
                            {
                                startIndex = sourceCode.IndexOf("<title>") + 7;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("|");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("</");
                            }
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"shortcode-content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("id=\"contactsection\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("END .content-block");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Barron's
                    if (u.Website.Trim().ToLower() == ("barron's"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("itemprop=\"headline\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("itemprop=\"author\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                                while (author.Contains("<a"))
                                {
                                    endIndex = author.IndexOf("<a");
                                    string firstAuthor = author.Substring(0, endIndex);
                                    author = author.Substring(endIndex + 2, author.Length - (endIndex + 2));
                                    startIndex = author.IndexOf(">") + 1;
                                    string secondAuthor = author.Substring(startIndex, author.Length - startIndex);
                                    author = firstAuthor + "\n" + secondAuthor;
                                }
                                author = author.Replace("</a>", "");
                            }

                            /* Story */
                            string story = "";
                            startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                            if (startIndex == -1)
                                story = "Need to subscribe";
                            else
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</article>");
                                story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<p"))
                                {
                                    endIndex = story.IndexOf("<p");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ins"))
                                {
                                    endIndex = story.IndexOf("<ins");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<style>"))
                                {
                                    endIndex = story.IndexOf("<style>");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</style>") + 8;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</style>", " ");
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</a>", " ");
                                story = story.Replace("<br />", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</ins>", " ");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&lsquo;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");
                            }

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Barron's (blog)
                    if (u.Website.Trim().ToLower() == ("barron's (blog)"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("<!-- article start -->");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("By");
                            if (startIndex != -1 || startIndex > 50)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                                while (author.Contains("<a"))
                                {
                                    endIndex = author.IndexOf("<a");
                                    string firstAuthor = author.Substring(0, endIndex);
                                    author = author.Substring(endIndex + 2, author.Length - (endIndex + 2));
                                    startIndex = author.IndexOf(">") + 1;
                                    string secondAuthor = author.Substring(startIndex, author.Length - startIndex);
                                    author = firstAuthor + "\n" + secondAuthor;
                                }
                                author = author.Replace("</a>", "");
                            }

                            endIndex = sourceCode.IndexOf("<!-- article end -->");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<p"))
                            {
                                endIndex = story.IndexOf("<p");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<dl"))
                            {
                                endIndex = story.IndexOf("<dl");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<dt"))
                            {
                                endIndex = story.IndexOf("<dt");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</dl>", " ");
                            story = story.Replace("</dt>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region BBC News
                    if (u.Website.Trim().ToLower() == ("bbc news"))
                    {
                        try
                        {
                            if (u.Link.Contains("news/live/"))
                            {
                                u.Author = "Undefined";
                                u.Story = "Media File";

                                db.SaveChanges();
                                saved++;
                            }
                            else
                            {
                                string sourceCode = WorkerClasses.getSourceCode(u.Link);

                                /* Find TITLE */
                                int startIndex = sourceCode.IndexOf("<title>") + 7;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("</");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&#8217;", "'");

                                /* Author */
                                string author = "Undefined";

                                /* Story */
                                startIndex = sourceCode.IndexOf("class=\"introduction\"");
                                if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"story-body\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<!-- / story-body");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"story-related\"");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("<em>");
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ins"))
                                {
                                    endIndex = story.IndexOf("<ins");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<style>"))
                                {
                                    endIndex = story.IndexOf("<style>");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</style>") + 8;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</style>", " ");
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</a>", " ");
                                story = story.Replace("<br />", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</ins>", " ");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&lsquo;", "'");
                                story = story.Replace("&#039;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();

                                db.SaveChanges();
                                saved++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region BDlive
                    if (u.Website.Trim().ToLower() == "bdlive")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);
                            if (sourceCode == "invalid") throw new EntityCommandExecutionException("404");

                            /* Refind TITLE */
                            int startIndex = sourceCode.IndexOf("<div class=\"articletitle\">") + 26;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex);

                            /* Author */
                            startIndex = sourceCode.IndexOf("<div class=\"meta\">") + 18;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf(",");
                            string author = sourceCode.Substring(0, endIndex);
                            while (author.Contains("<a"))
                            {
                                endIndex = author.IndexOf("<a");
                                string firstPart = author.Substring(0, endIndex);
                                author = author.Substring(endIndex + 2, author.Length - (endIndex + 2));
                                startIndex = author.IndexOf(">") + 1;
                                string secondPart = author.Substring(startIndex, author.Length - startIndex);
                                author = firstPart + "\n" + secondPart;
                                author = author.Replace("</a>", "");
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("<div class=\"cXenseParse\">") + 25;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</div>");
                            string story = sourceCode.Substring(0, endIndex);
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("404"))
                            {
                                u.Author = "Undefined";
                                u.Story = "Page Not Found";
                                db.SaveChanges();
                                saved++;
                            }
                            else
                                errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region BizNews
                    if (u.Website.Trim().ToLower().Contains("biznews"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("class=\"entry-title\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("id='nrelate_flyout_placeholder'");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"entry clearfix\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("::after");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</div"); 
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</article>");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<em"))
                            {
                                endIndex = story.IndexOf("<em");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<p"))
                            {
                                endIndex = story.IndexOf("<p");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<em>", "");
                            story = story.Replace("</em>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Bloomberg
                    if (u.Website.Trim().ToLower() == "bloomberg")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Refind TITLE */
                            int startIndex = sourceCode.IndexOf("article_title buffer") + 23;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex);

                            /* Author */
                            startIndex = sourceCode.IndexOf("itemprop='author'>") + 18;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();
                            if (author.Length > 50) author = author.Substring(0, 48);

                            /* Story */
                            startIndex = sourceCode.IndexOf("itemprop='articleBody'>") + 24;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</div>");
                            string story = sourceCode.Substring(0, endIndex);
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Brisbane Times
                    if (u.Website.Trim().ToLower().Contains("brisbane times"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            if (u.Link.Contains("media."))
                            {
                                u.Author = "Undefined";
                                u.Story = "This is a media file";
                            }
                            else
                            {
                                /* Find TITLE */
                                int startIndex = sourceCode.IndexOf("class=\"cN-headingPage\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("</");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&#8217;", "'");

                                /* Author */
                                string author = "Undefined";
                                startIndex = sourceCode.IndexOf("class=\"authorName\"");
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("</");
                                    author = sourceCode.Substring(0, endIndex).Trim();
                                }

                                /* Story */
                                startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("class:articleBody");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("<!--");
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ins"))
                                {
                                    endIndex = story.IndexOf("<ins");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<style>"))
                                {
                                    endIndex = story.IndexOf("<style>");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</style>") + 8;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</style>", " ");
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</a>", " ");
                                story = story.Replace("<br />", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</ins>", " ");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&lsquo;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();
                            }

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Businessweek
                    if (u.Website.Trim().ToLower() == "businessweek")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Refind TITLE */
                            int startIndex = sourceCode.IndexOf("name headline") + 15;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex);

                            /* Author */
                            startIndex = sourceCode.IndexOf("http://schema.org/Person") + 27;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();
                            if (author.Length > 50) author = author.Substring(0, 48);

                            /* Story */
                            startIndex = sourceCode.IndexOf("itemprop='articleBody'>") + 24;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</div>");
                            string story = sourceCode.Substring(0, endIndex);
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("::before", "");
                            story = story.Replace("::after", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Business Recorder
                    if (u.Website.Trim().ToLower() == "business recorder")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Refind TITLE */
                            int startIndex = sourceCode.IndexOf("class=\"title\">") + 14;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            if (sourceCode.IndexOf("href") != -1 && sourceCode.IndexOf("href") < 10)
                            {
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            }
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();

                            /* Author */
                            startIndex = sourceCode.IndexOf("<span class=\"author\">") + 22;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();

                            /* Story */
                            startIndex = sourceCode.IndexOf("<P") + 2;
                            if (sourceCode.IndexOf("<P") == -1)
                                startIndex = sourceCode.IndexOf("<p");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("</saqib>") + 8;
                            if (sourceCode.IndexOf("</saqib>") == -1)
                                startIndex = 0;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            if (sourceCode.IndexOf("<Center>") != -1)
                                endIndex = sourceCode.IndexOf("<Center>");
                            else
                                endIndex = sourceCode.IndexOf("<center>");
                            string story = sourceCode.Substring(0, endIndex);
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Business Standard
                    if (u.Website.Trim().ToLower() == "business standard")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Refind TITLE */
                            int startIndex = sourceCode.IndexOf("\"headline\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();

                            /* Author */
                            startIndex = sourceCode.IndexOf("byline mT5 mB5") + 16;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();
                            author = author.Replace("<strong>", "");
                            author = author.Replace("&nbsp;", " ").Trim();

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"articleBody\">") + 14;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</div");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            story = story.Replace("<br>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Business Spectator
                    if (u.Website.Trim().ToLower().Contains("business spectator"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title") + 6;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("class=\"meta--author\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                if (sourceCode.IndexOf("<a") < 15)
                                {
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                }
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            string story = "";
                            if (sourceCode.Contains("Please enable javascript to continue."))
                                story = "Blocked...";
                            else
                            {
                                startIndex = sourceCode.IndexOf("class=\"field--body\"");
                                if (startIndex == -1) startIndex = sourceCode.IndexOf("<div class=\"group-container\">");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("More from " + author);
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("Related articles");
                                story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ins"))
                                {
                                    endIndex = story.IndexOf("<ins");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<iframe"))
                                {
                                    endIndex = story.IndexOf("<iframe");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<td"))
                                {
                                    endIndex = story.IndexOf("<td");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<tr"))
                                {
                                    endIndex = story.IndexOf("<tr");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<th"))
                                {
                                    endIndex = story.IndexOf("<th");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<table"))
                                {
                                    endIndex = story.IndexOf("<table");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<font"))
                                {
                                    endIndex = story.IndexOf("<font");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<style>"))
                                {
                                    endIndex = story.IndexOf("<style>");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</style>") + 8;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</style>", "");
                                story = story.Replace("</span>", "");
                                story = story.Replace("</script>", "");
                                story = story.Replace("</a>", "");
                                story = story.Replace("<br />", "");
                                story = story.Replace("</ul>", "");
                                story = story.Replace("</li>", "");
                                story = story.Replace("<tr>", "");
                                story = story.Replace("</tr>", "");
                                story = story.Replace("<td>", "");
                                story = story.Replace("</td>", "");
                                story = story.Replace("<p>", "");
                                story = story.Replace("<p", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</ins>", "");
                                story = story.Replace("</div>", "");
                                story = story.Replace("</font>", "");
                                story = story.Replace("</i>", "");
                                story = story.Replace("</b>", "");
                                story = story.Replace("<i>", "");
                                story = story.Replace("<b>", "");
                                story = story.Replace("<strong>", "");
                                story = story.Replace("</strong>", "");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&lsquo;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&#8216;", "'");
                                story = story.Replace("&#8217;", "'");
                                story = story.Replace("&#8220;", "\"");
                                story = story.Replace("&#8221;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");
                                story = story.Replace("\r", "");
                                story = story.Replace("&mdash;", " - ");
                                story = story.Replace("</iframe>", "");
                                story = story.Replace("<li>", "");
                                story = story.Replace("<em>", "");
                                story = story.Replace("</em>", "");
                                story = story.Replace("</h4>", "");
                                story = story.Replace("<h4>", "");
                                story = story.Replace("</tr>", "");
                                story = story.Replace("<tbody>", "");
                                story = story.Replace("</td>", "");
                                story = story.Replace("</thread>", "");
                                story = story.Replace("</tbody>", "");
                                story = story.Replace("</th>", "");
                                story = story.Replace("</table>", "");
                                story = story.Replace("<h2 class=\"block__title\">", "");
                            }

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region BusinessWorld Online Edition
                    if (u.Website.Trim().ToLower() == "businessworld online edition")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Refind TITLE */
                            int startIndex = sourceCode.IndexOf("<h1>") + 4;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("<h4>") + 4;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</h4");
                            string headline = sourceCode.Substring(0, endIndex) + "    ";

                            startIndex = sourceCode.IndexOf("story_bottom") + 14;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            if (sourceCode.IndexOf("<table") < 500 && sourceCode.IndexOf("<table") != -1)
                            {
                                startIndex = sourceCode.IndexOf("</table>") + 8;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            }
                            endIndex = sourceCode.IndexOf("</div>");
                            string story = headline + sourceCode.Substring(0, endIndex).Trim();
                            story = story.Replace("<br />", "   ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Business Insider Australia
                    if (u.Website.Trim().ToLower().Contains("business insider australia"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("h1 class=\"first\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            startIndex = sourceCode.IndexOf("class=\"author\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("<a");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"story\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<hr");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Canada NewsWire (press release)
                    if (u.Website.Trim().ToLower().Contains("canada newswire"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("id=\"ReleaseContent\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("id=\"tags");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!-- End");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("id=\"comment-block");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region CAPA - Centre for Aviation
                    if (u.Website.Trim().ToLower().Contains("centre for aviation"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("id=\"article\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("id=\"related\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!-- no index");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<p"))
                            {
                                endIndex = story.IndexOf("<p");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&copy; CAPA", "");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region CNBC
                    if (u.Website.Trim().ToLower().Contains("cnbc"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title") + 6;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("meta name=\"author\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("content=\"") + 9;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("\"");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("<div class=\"group-container\">");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</article>");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<tr"))
                            {
                                endIndex = story.IndexOf("<tr");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<th"))
                            {
                                endIndex = story.IndexOf("<th");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("<li>", "");
                            story = story.Replace("</h4>", "");
                            story = story.Replace("<h4>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<tbody>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("</thread>", "");
                            story = story.Replace("</tbody>", "");
                            story = story.Replace("</th>", "");
                            story = story.Replace("</table>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region CommBank MyWealth
                    if (u.Website.Trim().ToLower() == "commbank mywealth")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex == -1 || endIndex > 100) endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("<!-- Article Body -->");
                            string story = "";
                            if (startIndex == -1) story = "Not Found.";
                            else
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<!-- Tag");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("<!--");
                                story = sourceCode.Substring(0, endIndex).Trim();
                            }
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Crikey
                    if (u.Website.Trim().ToLower() == ("crikey"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("class=\"entry-meta author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }
                            while (author.Contains("<a"))
                            {
                                endIndex = author.IndexOf("<a");
                                string firstPart = author.Substring(0, endIndex);
                                author = author.Substring(endIndex + 2, author.Length - (endIndex + 2));
                                startIndex = author.IndexOf(">") + 1;
                                string secondPart = author.Substring(startIndex, author.Length - startIndex);
                                author = firstPart + "\n" + secondPart;
                                author = author.Replace("</a>", "");
                            }
                            if (author.Contains("span")) author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("id='post-content");
                            if (startIndex == -1) startIndex = 0;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<!-- CLICK");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("id=\"topics");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</div");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<p"))
                            {
                                endIndex = story.IndexOf("<p");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("<em>", " ");
                            story = story.Replace("</em>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&thinsp;&#8212;&thinsp;", " - ");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("</blockquote>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region cricket.com.au
                    if (u.Website.Trim().ToLower().Contains("cricket.com.au"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title") + 6;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("<meta name=\"parsely-author\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("content=\"") + 9;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("\"");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }
                            if (author.Length == 0) author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"article-text\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("</script>");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</section");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<div class=\"row\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<tr"))
                            {
                                endIndex = story.IndexOf("<tr");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<th"))
                            {
                                endIndex = story.IndexOf("<th");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<object"))
                            {
                                endIndex = story.IndexOf("<object");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("aram name"))
                            {
                                endIndex = story.IndexOf("aram name");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 9, story.Length - (endIndex + 9));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("<li>", "");
                            story = story.Replace("<em>", "");
                            story = story.Replace("</em>", "");
                            story = story.Replace("</h4>", "");
                            story = story.Replace("<h4>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<tbody>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("</thread>", "");
                            story = story.Replace("</tbody>", "");
                            story = story.Replace("</th>", "");
                            story = story.Replace("</table>", "");
                            story = story.Replace("</object>", "");
                            story = story.Replace("brightcove.createExperiences();", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim(); 

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Economic Times
                    if (u.Website.Trim().ToLower().Contains("economic times"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Author */
                            string author = "Undefined";

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("\"multi-line-title-1\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("<title");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            string firstStory, secondStory, story = "";
                            startIndex = sourceCode.IndexOf("\"mod-a-body-first-para\"");
                            if (startIndex == -1)
                            {
                                startIndex = sourceCode.IndexOf("class=\"artText\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</div>");
                                story = sourceCode.Substring(0, endIndex).Trim();
                            }
                            else
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</div>");
                                firstStory = sourceCode.Substring(0, endIndex).Trim();

                                startIndex = sourceCode.IndexOf("\"mod-a-body-after-first-para\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</div>");
                                secondStory = sourceCode.Substring(0, endIndex).Trim();
                                story = firstStory + " " + secondStory;
                            }
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("<br>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region eFXnews
                    if (u.Website.Trim().ToLower() == "efxnews")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("property=\"dc:title\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "eFXnews.com";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"rich-text\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<script");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Finance News Network
                    if (u.Website.Trim().ToLower() == ("finance news network"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */ 
                            int startIndex = sourceCode.IndexOf("class=\"nItemTitle\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("<title>");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("style=\"min-height");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("class=\"BotShadowBg\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!-- FOOTER");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<input"))
                            {
                                endIndex = story.IndexOf("<input");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("<![endif]-->", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Global Trade Review (GTR)
                    if (u.Website.Trim().ToLower().Contains("global trade review"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Author */
                            string author = "Undefined";

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("id=\"content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("<h1>") + 4;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"article article-content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<!--");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Hellenic Shipping News Worldwide
                    if (u.Website.Trim().ToLower().Contains("hellenic shipping news worldwide"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Author */
                            string author = "Undefined";

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("\"name post-title entry-title\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("<");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"entry\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</div");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Intercooler
                    if (u.Website.Trim().ToLower().Contains("intercooler"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=\"author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"entry postcontent");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("style=\"clear:both\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<form");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", ""); 
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("<g:plusone size=\"medium\"></g:plusone>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region International Business Times AU
                    if (u.Website.Trim().ToLower() == "international business times au")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("<");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("class=\"article_writtenby\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("id=\"content\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"article-content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("class=\"tool\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!-- /.node -->"); 
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<blockquote"))
                            {
                                endIndex = story.IndexOf("<blockquote");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 11, story.Length - (endIndex + 11));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("<strong>", ""); 
                            story = story.Replace("</strong>", "");
                            story = story.Replace("</blockquote>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region ITWeb
                    if (u.Website.Trim().ToLower() == "itweb")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Author */
                            int startIndex = sourceCode.IndexOf("\"author\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("content=") + 8;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("/");
                            string author = sourceCode.Substring(0, endIndex).Trim();

                            /* Find TITLE */
                            startIndex = sourceCode.IndexOf("\"article-title\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("\"", "");

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"article-text\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<hr");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ul"))
                            {
                                endIndex = story.IndexOf("<ul");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<li"))
                            {
                                endIndex = story.IndexOf("<li");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("<br/>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Lexology (registration)
                    if (u.Website.Trim().ToLower().Contains("lexology"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("id=\"articleTitle\">");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&amp;", "&");

                            /* Author */
                            startIndex = sourceCode.IndexOf("class=\"byline\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"contributor\">");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();
                            while (author.Contains("<a"))
                            {
                                endIndex = author.IndexOf("<a");
                                string firstAuthor = author.Substring(0, endIndex);
                                author = author.Substring(endIndex + 2, author.Length - (endIndex + 2));
                                startIndex = author.IndexOf(">") + 1;
                                string secondAuthor = author.Substring(startIndex, author.Length - startIndex);
                                author = firstAuthor + "\n" + secondAuthor;
                            }
                            author = author.Replace("</a>", "").Trim();
                            author = author.Replace("&amp;", "&");
                            if (author.Length > 50) author = author.Substring(0, 48);

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"article-body\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("id=\"article-footnote\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("id=\"article-tags\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Livemint
                    if (u.Website.Trim().ToLower().Contains("livemint"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("class=\"sty_author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"rft_sty_kicker");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<!-- Body End");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"clr_both");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("<g:plusone size=\"medium\"></g:plusone>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region London South East
                    if (u.Website.Trim().ToLower().Contains("london south east"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"storyContent");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("Back to Alliance News");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<div class=\"FinNewsRelatedShares");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<br />");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", ""); 
                            story = story.Replace("googletag.cmd.push(function()", "");
                            story = story.Replace("{ googletag.display('div-gpt-ad-1353927354827-2'); });", "");
                            story = story.Replace("<g:plusone size=\"medium\"></g:plusone>", "");
                            story = story.Replace("<br clear=\"all\" />", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Loyalty360
                    if (u.Website.Trim().ToLower() == "loyalty360")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("'name_title'");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            startIndex = sourceCode.IndexOf("\"article-author\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"article copy\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</div");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ul"))
                            {
                                endIndex = story.IndexOf("<ul");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<li"))
                            {
                                endIndex = story.IndexOf("<li");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("<br/>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Malay Mail Online
                    if (u.Website.Trim().ToLower() == "malay mail online")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Author */
                            string author = "Undefined";

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("class=\"headlines\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"article-content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("class=\"comments\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("More Stories for You:");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = "";
                                if (story.Length >= startIndex)
                                    secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            endIndex = story.IndexOf("jwplayer");
                            if (startIndex != -1) story = story.Substring(0, endIndex);
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region MarketWatch
                    if (u.Website.Trim().ToLower() == "marketwatch")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Author */
                            int startIndex = sourceCode.IndexOf("\"author\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("content=") + 8;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf(">");
                            string author = sourceCode.Substring(0, endIndex).Trim();
                            author = author.Replace("\"", "").Trim();
                            if (author.Length > 50) author = author.Substring(0, 48);

                            /* Find TITLE */
                            startIndex = sourceCode.IndexOf("\"article-headline\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'").Trim();

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"article-body\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</article");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ul"))
                            {
                                endIndex = story.IndexOf("<ul");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<li"))
                            {
                                endIndex = story.IndexOf("<li");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("<br/>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Markets Insider
                    if (u.Website.Trim().ToLower() == "markets insider")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"entry-content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf(".entry-content");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!-- ");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"entry-action");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("(adsbygoogle = window.adsbygoogle || []).push({});", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Mideast Time
                    if (u.Website.Trim().ToLower() == "mideast time")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("\"entry-title\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            startIndex = sourceCode.IndexOf("rel=\"author\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"entry\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("\"clear:both\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<form");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ul"))
                            {
                                endIndex = story.IndexOf("<ul");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<li"))
                            {
                                endIndex = story.IndexOf("<li");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("<br/>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region MiningNews.net (subscription)
                    if (u.Website.Trim().ToLower().Contains("miningnews.net"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("c2Heads");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("storyHead");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("storyshare");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("bodytext");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ul"))
                            {
                                endIndex = story.IndexOf("<ul");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<li"))
                            {
                                endIndex = story.IndexOf("<li");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("<p class=\"bodyLineBreak\">", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            /* Author */
                            startIndex = sourceCode.IndexOf("'storyAuthors',") + 14;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("'") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("'");
                            string author = sourceCode.Substring(0, endIndex).Trim();

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Money Morning Australia
                    if (u.Website.Trim().ToLower() == ("money morning australia"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=\"author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"entry\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("id=\"dd_end");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!--/post");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("id=\"comment\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Moodys.com (press release) (subscription)
                    if (u.Website.Trim().ToLower().Contains("moodys.com"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            int startIndex = sourceCode.IndexOf("id=\"mdcRDBottomContent\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("<html");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<xml>");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</div>");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", ""); 
                            story = story.Replace("\r", "");
                            story = story.Replace("<H4>", "");
                            story = story.Replace("</H4>", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("<br clear=\"all\" />", "");

                            /* Find TITLE */
                            startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Morningstar.com
                    if (u.Website.Trim().ToLower().Contains("morningstar"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Author */
                            string author = "Undefined";

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("\"titleLink\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("<title");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("p xml:lang=");  
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("<P>");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"mainContent");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("The information contained within is for educational");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<div id=\"RelateArticle\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"aadsection_a1\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<p"))
                            {
                                endIndex = story.IndexOf("<p");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!"))
                            {
                                endIndex = story.IndexOf("<!");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<SCRIPT"))
                            {
                                endIndex = story.IndexOf("<SCRIPT");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("<b>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Motley Fool Australia
                    if (u.Website.Trim().ToLower() == "motley fool australia")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<hgroup>");
                            int endIndex = 0;
                            if (startIndex == -1)
                            {
                                startIndex = sourceCode.IndexOf("<title>") + 6;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                            }
                            else
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("<h2>") + 4;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</h2");
                            }
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            startIndex = sourceCode.IndexOf("class=\"byline\"") + 15;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();
                            if (author.Length > 50) author = author.Substring(0, 48);

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"full_content\"") + 15;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            if (sourceCode.IndexOf("<p>") != -1)
                                startIndex = sourceCode.IndexOf("<p>") + 3;
                            else
                                startIndex = sourceCode.IndexOf("<P>") + 3;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</div");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            story = story.Replace("<br />", "   ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("500"))
                            {
                                u.Author = "Not Found";
                                u.Story = "Page Not Found";

                                db.SaveChanges();
                                saved++;
                            }
                            else
                                errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Nasdaq
                    if (u.Website.Trim().ToLower() == "nasdaq")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* TITLE */
                            int startIndex = sourceCode.IndexOf("itemprop=\"headline\"");
                            if (startIndex == -1)
                            {
                                u.Author = "Undefined";
                                u.Story = "Page Not Found";

                                db.SaveChanges();
                                saved++;
                            }
                            else
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("</");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&#8217;", "'");

                                /* Story */
                                startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<!--");
                                string story = sourceCode.Substring(0, endIndex).Trim();

                                /* Author */
                                string author = "Undefined";
                                startIndex = sourceCode.IndexOf("class=\"nitfby\"");
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("</");
                                    author = sourceCode.Substring(0, endIndex).Trim();
                                }

                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</script>") + 9;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf("</span>") + 7;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf("</div>") + 6;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ul"))
                                {
                                    endIndex = story.IndexOf("<ul");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<li"))
                                {
                                    endIndex = story.IndexOf("<li");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("<br />", " ");
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("<div>", "");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&#39;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");
                                while (story.Contains("<p"))
                                {
                                    endIndex = story.IndexOf("<p");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();

                                db.SaveChanges();
                                saved++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region New Straits Times Online
                    if (u.Website.Trim().ToLower() == "new straits times online")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("\"dc:title\" datatype");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("<a");
                            if (startIndex > 50) { startIndex = sourceCode.IndexOf("\"dc:title\" datatype"); }
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("property=\"content:encoded\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("class=\"clearfix\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = "";
                                if (story.IndexOf("</div>") != -1)
                                    secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ul"))
                            {
                                endIndex = story.IndexOf("<ul");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<li"))
                            {
                                endIndex = story.IndexOf("<li");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            /* Author */
                            string author = "Undefined";

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region NEWS.com.au
                    if (u.Website.Trim().ToLower() == ("news.com.au"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("h1 class=\"heading\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("-->") + 3;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("<!--");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("class=\"author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }
                            if (author == "") author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"story-intro\"");
                            if (startIndex == -1)
                            {
                                sourceCode = WorkerClasses.getSourceCode(u.Link);
                                startIndex = sourceCode.IndexOf("class=\"story-intro\"");
                            }
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf(".story-body");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!--");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region New Zealand Herald
                    if (u.Website.Trim().ToLower() == "new zealand herald")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex > 40 || endIndex == -1) endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Not Found";

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"articleBody\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("\"detailsLarge articleEmailLink\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Ninemsn
                    if (u.Website.Trim().ToLower().Contains("ninemsn"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            if (u.Link.Contains("alerts.news.ninemsn.com.au"))
                            {
                                u.Author = "Undefined";
                                u.Story = "Unavailable";
                            }
                            else
                            {
                                /* Find TITLE */
                                int startIndex = sourceCode.IndexOf("<title>") + 7;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("</");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&#8217;", "'");
                                title = title.Replace("&#8220;", "\"");
                                title = title.Replace("&#8221;", "\"");

                                /* Author */
                                string author = "Undefined";
                                startIndex = sourceCode.IndexOf("\"AUTHOR\"");
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf("content=\"") + 9;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf(",");
                                    author = sourceCode.Substring(0, endIndex).Trim();
                                }

                                /* Story */
                                startIndex = sourceCode.IndexOf("<div id=\"article_body\">");
                                if (startIndex == -1) startIndex = sourceCode.IndexOf("<div id=\"article0_body\">");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<span id=\"timeNow\">");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("<span id=\"dateNow\">");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("<div id=\"article_mooter\">");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("<div class=\"ShareToolBarBottom\">");
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ins"))
                                {
                                    endIndex = story.IndexOf("<ins");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<iframe"))
                                {
                                    endIndex = story.IndexOf("<iframe");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<td"))
                                {
                                    endIndex = story.IndexOf("<td");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<table"))
                                {
                                    endIndex = story.IndexOf("<table");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<font"))
                                {
                                    endIndex = story.IndexOf("<font");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<style>"))
                                {
                                    endIndex = story.IndexOf("<style>");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</style>") + 8;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</style>", "");
                                story = story.Replace("</span>", "");
                                story = story.Replace("</script>", "");
                                story = story.Replace("</a>", "");
                                story = story.Replace("<br />", "");
                                story = story.Replace("</ul>", "");
                                story = story.Replace("</li>", "");
                                story = story.Replace("<tr>", "");
                                story = story.Replace("</tr>", "");
                                story = story.Replace("<td>", "");
                                story = story.Replace("</td>", "");
                                story = story.Replace("<p>", "");
                                story = story.Replace("<p", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</ins>", "");
                                story = story.Replace("</div>", "");
                                story = story.Replace("</font>", "");
                                story = story.Replace("</i>", "");
                                story = story.Replace("</b>", "");
                                story = story.Replace("<i>", "");
                                story = story.Replace("<b>", "");
                                story = story.Replace("<strong>", "");
                                story = story.Replace("</strong>", "");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&lsquo;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&#8216;", "'");
                                story = story.Replace("&#8217;", "'");
                                story = story.Replace("&#8220;", "\"");
                                story = story.Replace("&#8221;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");
                                story = story.Replace("\r", "");
                                story = story.Replace("&mdash;", " - ");
                                story = story.Replace("</iframe>", "");
                                story = story.Replace("<li>", "");
                                story = story.Replace("</h4>", "");
                                story = story.Replace("<h4>", "");

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();
                            }
                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Professional Planner
                    if (u.Website.Trim().ToLower() == "professional planner")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("class=\"float_left fnt_fgb\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"entrycontent_single");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<hr");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Proactive Investors Australia
                    if (u.Website.Trim().ToLower().Contains("proactive investors australia"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");
                            title = title.Replace("&#039;", "'");

                            /* Author */
                            string author = "Proactive Investors";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"imgContentArticle");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"company_info");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("Proactive Investors");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("Proactive Investors Australia is the market leader");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("Sign up to Proactive Investors");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<i"))
                            {
                                endIndex = story.IndexOf("<i");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<object"))
                            {
                                endIndex = story.IndexOf("<object");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<figure"))
                            {
                                endIndex = story.IndexOf("<figure");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<figcaption"))
                            {
                                endIndex = story.IndexOf("<figcaption");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 11, story.Length - (endIndex + 11));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("aram name"))
                            {
                                endIndex = story.IndexOf("aram name");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 9, story.Length - (endIndex + 9));
                                startIndex = story.IndexOf("/>") + 2;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</object>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</figure>", "");
                            story = story.Replace("</figcaption>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&#34;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("&#38;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("<h2>", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("style=\"text-align: justify;\">", "");
                            story = story.Replace("class=\"caption\">", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Property Mentor
                    if (u.Website.Trim().ToLower().Contains("property mentor"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=\"author\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("<div class=\"entry\">");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("<table>");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<div class=\"clear\">");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<div class=\"share_box\">");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region PR Newswire (press release)
                    if (u.Website.Trim().ToLower().Contains("pr newswire"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("itemprop=\"articleBody\">");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("<!-- title");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("To register");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("SOURCE");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!--startclickprintexclude-->");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - "); 
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("itemprop=\"articleBody\">", "");
                            story = story.Replace("<br/>", "");
                            story = story.Replace("&#160;", "");
                            story = story.Replace("&#8211;", ",");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region RenewEconomy
                    if (u.Website.Trim().ToLower() == "reneweconomy")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("class=\"post-title\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("<a");
                            if (startIndex > 50) { startIndex = sourceCode.IndexOf("class=\"post-title\""); }
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            startIndex = sourceCode.IndexOf("rel=\"author\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"WordSection1\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"pf-content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<!--");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ul"))
                            {
                                endIndex = story.IndexOf("<ul");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<li"))
                            {
                                endIndex = story.IndexOf("<li");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("<span lang=\"EN-GB\">", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Reuters && Reuters UK
                    if (u.Website.Trim().ToLower() == "reuters" || u.Website.Trim().ToLower() == "reuters uk")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("column1 gridPanel grid8");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("<h1>") + 4;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</h1");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("articleText") + 13;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<div");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            story = story.Replace("</span>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Reuters Key Development
                    if (u.Website.Trim().ToLower() == "reuters key development")
                    {
                        try
                        {
                            string linkToPage = u.Link;
                            int startIndex = linkToPage.IndexOf("q=") + 2;
                            int endIndex = 0;
                            if (startIndex != -1)
                            {
                                linkToPage = linkToPage.Substring(startIndex, linkToPage.Length - startIndex);
                                endIndex = linkToPage.IndexOf("&");
                                linkToPage = linkToPage.Substring(0, endIndex);
                            }
                            linkToPage = linkToPage.Replace("%3A", ":");
                            linkToPage = linkToPage.Replace("%2F", "/");
                            string sourceCode = WorkerClasses.getSourceCode(linkToPage);

                            if (sourceCode.ToLower() != "invalid")
                            {
                                /* Find TITLE */
                                startIndex = sourceCode.IndexOf("column2 gridPanel grid6");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("<h1>") + 4;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</h1");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&#8217;", "'");

                                /* Author */
                                string author = "Undefined";

                                /* Story */
                                startIndex = sourceCode.IndexOf("gridPanel grid6");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("<p>") + 3;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</div");
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                story = story.Replace("</span>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&#8217;", "'");
                                story = story.Replace("&#8220;", "\"");
                                story = story.Replace("&#8221;", "\"");
                                story = story.Replace("&amp;", "&");

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story;

                                db.SaveChanges();
                                saved++;
                            }
                            else
                            {
                                u.Author = "Not Found";
                                u.Story = "Not Found";

                                db.SaveChanges();
                                saved++;
                            }
                        }
                        catch (Exception ex)
                        {
                            string errorMsg = ex.Message;
                            if (errorMsg.Contains("500") && errorMsg.ToLower().Contains("internal server"))
                            {
                                u.Author = "Not Found";
                                u.Story = "Page Not Found";

                                db.SaveChanges();
                                saved++;
                            }
                            else
                                errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region RTT News
                    if (u.Website.Trim().ToLower().Contains("rtt news"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "RTT Staff Writer";

                            /* Story */
                            startIndex = sourceCode.IndexOf("id=\"divBody\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<div id=\"divOutBrain\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</article>");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("</table>", "");
                            story = story.Replace("&#101;ditor&#105;al&#64;rttn&#101;&#119;s&#46;&#99;om", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Scoop.co.nz
                    if (u.Website.Trim().ToLower().Contains("scoop.co.nz"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            string title = ""; int endIndex = -1;
                            int startIndex = sourceCode.IndexOf("meta name=\"title\"");
                            if (startIndex == -1)
                            {
                                startIndex = sourceCode.IndexOf("<title>") + 7;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("/>");
                                title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&raquo", ":");
                            }
                            else
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("content=") + 8;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("/>");
                                title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("\"", "");
                            }

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("id=\"article\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("class=\"cleaner\"");
                            }
                            else if (sourceCode.IndexOf("BusinessDesk</p>") != -1)
                            {
                                startIndex = sourceCode.IndexOf("BusinessDesk</p>");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<p>(BusinessDesk)");
                            }
                            else if (sourceCode.IndexOf("class=\"entrybody\"") != -1)
                            {
                                startIndex = sourceCode.IndexOf("class=\"entrybody\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<!-- [entrybody]");
                            }
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</table>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<tr"))
                            {
                                endIndex = story.IndexOf("<tr");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</center>", " ");
                            story = story.Replace("<center>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("</a>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("&#8211;", "-");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Seeking Alpha
                    if (u.Website.Trim().ToLower().Contains("seeking alpha"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=\'author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"summary_content\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("id=\"article_source");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!--googleoff");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"mc_follow_up mc_list_cont\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Sleekmoney
                    if (u.Website.Trim().ToLower() == ("sleekmoney"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=\"author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"entry postcontent");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("<p>") + 3;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("Begin Footer");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("clear:both");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Stockhouse
                    if (u.Website.Trim().ToLower().Contains("stockhouse"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<h1>") + 4;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            string story = "";
                            startIndex = sourceCode.IndexOf("class=\"content\""); 
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("id=\"story\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"news-article\"");
                            if (sourceCode.Contains("An error has occurred.")) story = "An error has occurred.";
                            else
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<div class=\"article-rating\">");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("<hr class=\"thick\"/>");
                                story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ins"))
                                {
                                    endIndex = story.IndexOf("<ins");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<iframe"))
                                {
                                    endIndex = story.IndexOf("<iframe");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<link"))
                                {
                                    endIndex = story.IndexOf("<link");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<td"))
                                {
                                    endIndex = story.IndexOf("<td");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<table"))
                                {
                                    endIndex = story.IndexOf("<table");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<font"))
                                {
                                    endIndex = story.IndexOf("<font");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<style>"))
                                {
                                    endIndex = story.IndexOf("<style>");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</style>") + 8;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</style>", "");
                                story = story.Replace("</span>", "");
                                story = story.Replace("</script>", "");
                                story = story.Replace("</a>", "");
                                story = story.Replace("<br />", "");
                                story = story.Replace("</ul>", "");
                                story = story.Replace("</li>", "");
                                story = story.Replace("<tr>", "");
                                story = story.Replace("</tr>", "");
                                story = story.Replace("<td>", "");
                                story = story.Replace("</td>", "");
                                story = story.Replace("<p>", "");
                                story = story.Replace("<p", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</ins>", "");
                                story = story.Replace("</div>", "");
                                story = story.Replace("</font>", "");
                                story = story.Replace("</i>", "");
                                story = story.Replace("</b>", "");
                                story = story.Replace("<i>", "");
                                story = story.Replace("<b>", "");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&lsquo;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&#8216;", "'");
                                story = story.Replace("&#8217;", "'");
                                story = story.Replace("&#8220;", "\"");
                                story = story.Replace("&#8221;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");
                                story = story.Replace("\r", "");
                                story = story.Replace("&mdash;", " - ");
                                story = story.Replace("</iframe>", "");
                                story = story.Replace("</table>", "");
                                story = story.Replace("<html>", "");
                                story = story.Replace("<body>", "");
                                story = story.Replace("<h2>", "");
                                story = story.Replace("</h2>", "");
                                story = story.Replace("<br>", "");
                                story = story.Replace("<strong>", "");
                                story = story.Replace("</strong>", "");
                                story = story.Replace("</body>", "");
                                story = story.Replace("</html>", "");
                                story = story.Replace("</head>", "");
                                story = story.Replace("</sup>", "");
                                story = story.Replace("<sup>", "");
                                story = story.Replace("&#xA0;", "");
                                story = story.Replace("<hr class=\"thick\"/>", "");
                            }

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Stuff.co.nz
                    if (u.Website.Trim().ToLower().Contains("stuff.co.nz"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title") + 6;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("class=\"storycredit\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                if (sourceCode.IndexOf("<a") < 15)
                                {
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                }
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }
                            if (author.Length == 0) author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("name=storybody");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("</script>");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("Ad Feedback");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<div class=\"story-footer-ad\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<tr"))
                            {
                                endIndex = story.IndexOf("<tr");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<th"))
                            {
                                endIndex = story.IndexOf("<th");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("<li>", "");
                            story = story.Replace("<em>", "");
                            story = story.Replace("</em>", "");
                            story = story.Replace("</h4>", "");
                            story = story.Replace("<h4>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<tbody>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("</thread>", "");
                            story = story.Replace("</tbody>", "");
                            story = story.Replace("</th>", "");
                            story = story.Replace("</table>", "");
                            story = story.Replace("<h2 class=\"block__title\">", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Sun.Star
                    if (u.Website.Trim().ToLower().Contains("sun.star"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("property=\"og:title\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("content=") + 8;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("/>");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("\"", "");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"base\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<!-- start vicomi smileys only -->");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</table>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("/div>") + 5;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("/ins>") + 4;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("<br/>", " ");
                            story = story.Replace("<br>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("/center>", " ");
                            story = story.Replace("</center>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region South China Morning Post (subscription)
                    if (u.Website.Trim().ToLower().Contains("south china morning post"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Author */
                            int startIndex = sourceCode.IndexOf("\"authors\":") + 10;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf(",");
                            string author = sourceCode.Substring(0, endIndex).Trim();
                            author = author.Replace("\"", "");

                            /* Find TITLE */
                            startIndex = sourceCode.IndexOf("itemprop=\"name headline\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</h1");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("content:encoded") + 17;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</div");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            story = story.Replace("</span>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Sydney Morning Herald
                    if (u.Website.Trim().ToLower() == "sydney morning herald")
                    {
                        try
                        {
                            string linkToPage = u.Link;
                            string sourceCode = WorkerClasses.getSourceCode(linkToPage);

                            /* Author */
                            int startIndex = sourceCode.IndexOf("author:") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf(",");
                            string author = sourceCode.Substring(0, endIndex).Trim();
                            author = author.Replace("\"", "");
                            author = author.Replace("'", "").Trim();
                            if (author == "") author = "Undefined";
                            if (author.Length > 50) author = author.Substring(0, 48);

                            /* Find TITLE */
                            startIndex = sourceCode.IndexOf("cN-headingPage");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"articleBody\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<!-- class:articleBody");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("<div>", "");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Trim();

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Techsonian (press release)
                    if (u.Website.Trim().ToLower().Contains("techsonian"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);
                            StreamWriter sw = new StreamWriter("News_Article.txt");
                            sw.Write(sourceCode);
                            sw.Close();

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8211;", "-");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=\"author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"main_content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("style=\"font-size:");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"cent_ad\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&#8243;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("&#8211;", "-");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region The Canberra Times
                    if (u.Website.Trim().ToLower() == "the canberra times")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex > 50 || endIndex == -1) endIndex = sourceCode.IndexOf("<");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            startIndex = sourceCode.IndexOf("author:") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf(",");
                            if (endIndex > 40 || endIndex == -1) endIndex = sourceCode.IndexOf("<");
                            string author = sourceCode.Substring(0, endIndex).Trim();
                            author = author.Replace("'", "").Trim();
                            if (author.Length > 50) author = author.Substring(0, 48);

                            /* Story */
                            startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("class:articleBody");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region The Australian Financial Review
                    if (u.Website.Trim().ToLower() == "the australian financial review")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("\"headline\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("<h1>") + 4;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</h1");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("\"story_content\"");
                            if (startIndex == -1)
                            {
                                startIndex = sourceCode.IndexOf("Disable Authentication");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("style=\"padding");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            }
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("\"editor_details\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }
                            if (author == "") { author = "Undefined"; }
                            if (author.Length > 50) author = author.Substring(0, 48);

                            endIndex = sourceCode.IndexOf("<!-- Story tags and bio -->");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("</div");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf("</span>") + 7;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ul"))
                            {
                                endIndex = story.IndexOf("<ul");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("</ul>") + 5;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&ndash;", "-");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story;

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region The Age
                    if (u.Website.Trim().ToLower() == "the age")
                    {
                        try
                        {
                            if (u.Link.Contains("http://media."))
                            {
                                u.Author = "Undefined";
                                u.Story = "This is a media link";

                                db.SaveChanges();
                                saved++;
                            }
                            else
                            {
                                string sourceCode = WorkerClasses.getSourceCode(u.Link);

                                /* Find TITLE */
                                int startIndex = sourceCode.IndexOf("class=\"cN-headingPage\"");
                                int endIndex = 0; string title = "";
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("</");
                                    title = sourceCode.Substring(0, endIndex).Trim();
                                }
                                else
                                {
                                    startIndex = sourceCode.IndexOf("<title>") + 7;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("</title");
                                    title = sourceCode.Substring(0, endIndex).Trim();
                                }
                                title = title.Replace("&#8217;", "'");

                                /* Author */
                                startIndex = sourceCode.IndexOf("class=\"authorName\"");
                                string author = "Undefined";
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("</");
                                    author = sourceCode.Substring(0, endIndex).Trim();
                                    while (author.Contains("<a"))
                                    {
                                        endIndex = author.IndexOf("<a");
                                        string firstAuthor = author.Substring(0, endIndex);
                                        startIndex = sourceCode.IndexOf(">") + 1;
                                        author = author.Substring(startIndex, author.Length - startIndex);
                                    }
                                    if (author.Length > 50) author = author.Substring(0, 48);
                                }

                                /* Story */
                                startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                                if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"articleBody\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("class:articleBody");
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</script>") + 9;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf("</div>") + 6;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("</a>", "");
                                story = story.Replace("<em>", "");
                                story = story.Replace("</em>", "");
                                story = story.Replace("<strong>", "");
                                story = story.Replace("</strong>", "");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&#39;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();

                                db.SaveChanges();
                                saved++;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("410") || ex.Message.Contains("500"))
                            {
                                u.Title = "Not Found";
                                u.Author = "Undefined";
                                u.Story = "Page Not Found";

                                db.SaveChanges();
                                saved++;
                            }
                            else
                                errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region The Conversation AU, UK, US
                    if (u.Website.Trim().ToLower().Contains("the conversation"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=\"author");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();

                                if (author.LastIndexOf(">") != -1)
                                {
                                    startIndex = author.LastIndexOf(">") + 1;
                                    author = author.Substring(startIndex, author.Length - startIndex);
                                }
                            }
                            if (author == "") author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("id=\"related-content");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<aside");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region The Guardian
                    if (u.Website.Trim().ToLower() == ("the guardian"))
                    {
                        try
                        {
                            if (u.Link.Contains("video/"))
                            {
                                u.Author = "Undefined";
                                u.Story = "This is a video file";

                                db.SaveChanges();
                                saved++;
                            }
                            else
                            {
                                string sourceCode = WorkerClasses.getSourceCode(u.Link);

                                /* Find TITLE */
                                int startIndex = sourceCode.IndexOf("<title>") + 7;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("</");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("|");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&#8217;", "'");
                                title = title.Replace("&#8220;", "\"");
                                title = title.Replace("&#8221;", "\"");

                                /* Story */
                                startIndex = sourceCode.IndexOf("class=\"flexible-content-body");
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("<script type=");
                                    if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"trackable-component");
                                    if (endIndex == -1) endIndex = sourceCode.IndexOf("id=\"related");
                                }
                                else
                                {
                                    startIndex = sourceCode.IndexOf("itemprop=\"articleBody");
                                    if (startIndex == -1) startIndex = sourceCode.IndexOf("id=\"article-body");
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("class=\"submeta-container");
                                    if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"after-article");
                                    if (endIndex == -1) endIndex = sourceCode.IndexOf("</div>");
                                }
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ins"))
                                {
                                    endIndex = story.IndexOf("<ins");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<style>"))
                                {
                                    endIndex = story.IndexOf("<style>");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</style>") + 8;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</style>", " ");
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</a>", " ");
                                story = story.Replace("<br />", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</ins>", " ");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&lsquo;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");
                                story = story.Replace("<b>", "");
                                story = story.Replace("</b>", "");
                                story = story.Replace("<strong>", "");
                                story = story.Replace("</strong>", "");

                                /* Author */
                                string author = "Undefined";
                                startIndex = sourceCode.IndexOf("rel=\"author");
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("</");
                                    author = sourceCode.Substring(0, endIndex).Trim();
                                }
                                if (author == "") author = "Undefined";

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();

                                db.SaveChanges();
                                saved++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    
                    }
                    #endregion

                    #region The Land Newspaper
                    if (u.Website.Trim().ToLower().Contains("the land newspaper"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");
                            title = title.Replace("&#039;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("id=\"headerBar");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("id=\"storycontent");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"article-container");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<div id=\"source-bar");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"content-bar");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<i"))
                            {
                                endIndex = story.IndexOf("<i");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<object"))
                            {
                                endIndex = story.IndexOf("<object");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<figure"))
                            {
                                endIndex = story.IndexOf("<figure");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<figcaption"))
                            {
                                endIndex = story.IndexOf("<figcaption");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 11, story.Length - (endIndex + 11));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("aram name"))
                            {
                                endIndex = story.IndexOf("aram name");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 9, story.Length - (endIndex + 9));
                                startIndex = story.IndexOf("/>") + 2;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</object>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</figure>", "");
                            story = story.Replace("</figcaption>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&#34;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("&#38;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("class=\"article-abstract\">", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region The Motley Fool
                    if (u.Website.Trim().ToLower() == ("the motley fool"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Story */
                            startIndex = sourceCode.IndexOf("id=\"full_content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("method=\"POST\"");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("id=\"excerpt_content");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("id=\"post_footer");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }  
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("</iframe>"))
                            {
                                startIndex = story.IndexOf("</iframe>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = secondPart;
                            }
                            while (story.Contains("<li"))
                            {
                                endIndex = story.IndexOf("<li");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            if (story.Contains("wdsb-share-box"))
                            {
                                endIndex = story.IndexOf("wdsb-share-box");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 14, story.Length - (endIndex + 14));
                                startIndex = story.IndexOf("/div>") + 5;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&8217;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");
                            if (story.Contains("<style"))
                            {
                                startIndex = story.IndexOf("<li>") + 4;
                                story = story.Substring(startIndex, story.Length - startIndex);
                            }

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=\"author\">");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }
                            if (author == "") author = "Undefined";

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region The National Business Review (subscription)
                    if (u.Website.Trim().ToLower().Contains("the national business review"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");
                            title = title.Replace("&#039;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("class=\"submitted");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"field-item even");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"article-container");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<div id=\"bottom-action-wrapper");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("tag-title");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<i"))
                            {
                                endIndex = story.IndexOf("<i");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<object"))
                            {
                                endIndex = story.IndexOf("<object");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<figure"))
                            {
                                endIndex = story.IndexOf("<figure");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<figcaption"))
                            {
                                endIndex = story.IndexOf("<figcaption");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 11, story.Length - (endIndex + 11));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("aram name"))
                            {
                                endIndex = story.IndexOf("aram name");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 9, story.Length - (endIndex + 9));
                                startIndex = story.IndexOf("/>") + 2;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</object>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("</strong>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</figure>", "");
                            story = story.Replace("</figcaption>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&#34;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("&#38;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", ""); 
                            story = story.Replace("class=\"article-abstract\">", "");
                            story = story.Replace("GA_googleFillSlot(\"HP_ATF_300*250_1\");", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region The West Australian
                    if (u.Website.Trim().ToLower().Contains("the west australian"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("page-header-aside-source");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("itemprop=\"articleBody");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"article-container");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<a class=\"article-source-ref");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("The West Australian");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<object"))
                            {
                                endIndex = story.IndexOf("<object");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<figure"))
                            {
                                endIndex = story.IndexOf("<figure");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<figcaption"))
                            {
                                endIndex = story.IndexOf("<figcaption");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 11, story.Length - (endIndex + 11));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("aram name"))
                            {
                                endIndex = story.IndexOf("aram name");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 9, story.Length - (endIndex + 9));
                                startIndex = story.IndexOf("/>") + 2;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</object>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</figure>", "");
                            story = story.Replace("</figcaption>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&#34;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("&#38;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", ""); 
                            story = story.Replace("class=\"article-abstract\">", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Ticker Report
                    if (u.Website.Trim().ToLower() == "ticker report")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            if (sourceCode != "invalid")
                            {
                                /* Find TITLE */
                                int startIndex = sourceCode.IndexOf("<title>");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("|");
                                if (endIndex > 50 || endIndex == -1) endIndex = sourceCode.IndexOf("</");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&quot;", "\"");

                                /* Author */
                                startIndex = sourceCode.IndexOf("rel=\"author\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                string author = sourceCode.Substring(0, endIndex).Trim();

                                /* Story */
                                startIndex = sourceCode.IndexOf("class=\"entry\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<p style=\"clear:both\"");
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</script>") + 9;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf("</div>") + 6;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&#39;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();
                            }
                            else
                            {
                                u.Author = "Undefined";
                                u.Story = "Invalid";
                            }

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Tulsa World
                    if (u.Website.Trim().ToLower() == ("tulsa world"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("Tulsa World", "");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"content\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("id=\"dd_end");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<!-- (END)");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("class=\"page_navigation");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            if (story.Contains("Subscription Required"))
                            {
                                endIndex = story.IndexOf("Subscription Required");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 21, story.Length - (endIndex + 21));
                                startIndex = story.IndexOf("socialMedia:") + 12;
                                if (startIndex == -1) startIndex = story.IndexOf("resurrector:true") + 17;
                                string secondPart = "";
                                if (startIndex != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", " ");
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</a>", " ");
                            story = story.Replace("<br />", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Tuoitrenews
                    if (u.Website.Trim().ToLower() == "tuoitrenews")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("class=\"title-type-1\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("<");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";

                            /* Story */
                            startIndex = sourceCode.IndexOf("class=\"main-content-inner\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("class=\"clear\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</table>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = "";
                                if (story.IndexOf("</div>") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region WallStreet Scope
                    if (u.Website.Trim().ToLower() == ("wallstreet scope"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            if (sourceCode != "invalid")
                            {
                                /* Find TITLE */
                                int startIndex = sourceCode.IndexOf("class=\"entry-title\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("</");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&#8217;", "'");

                                /* Author */
                                string author = "Undefined";
                                startIndex = sourceCode.IndexOf("itemprop=\"author");
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    endIndex = sourceCode.IndexOf("</");
                                    author = sourceCode.Substring(0, endIndex).Trim();
                                }

                                /* Story */
                                startIndex = sourceCode.IndexOf("<p>WallStreet Scope");
                                if (startIndex == -1) startIndex = sourceCode.IndexOf(">WallStreet Scope");
                                if (startIndex == -1) startIndex = sourceCode.IndexOf("<p>");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("id=\"tabloola-below-article");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("font-size");
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ins"))
                                {
                                    endIndex = story.IndexOf("<ins");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<style>"))
                                {
                                    endIndex = story.IndexOf("<style>");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</style>") + 8;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</style>", " ");
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</a>", " ");
                                story = story.Replace("<br />", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</ins>", " ");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&lsquo;", "'");
                                story = story.Replace("&#8217", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");
                                story = story.Replace("style=\"", "");

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();
                            }
                            else
                            {
                                u.Author = "Undefined";
                                u.Story = "Invalid";
                            }
                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Watch List News (press release)
                    if (u.Website.Trim().ToLower().Contains("watch list news"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex > 50 || endIndex == -1) endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&quot;", "\"");

                            /* Author */
                            startIndex = sourceCode.IndexOf("rel=\"author\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string author = sourceCode.Substring(0, endIndex).Trim();

                            /* Story */
                            startIndex = sourceCode.IndexOf("entry postcontent");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<p style=\"clear:both\">");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Winston View
                    if (u.Website.Trim().ToLower().Contains("winston view"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>") + 7;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");
                            title = title.Replace("&#8220;", "\"");
                            title = title.Replace("&#8221;", "\"");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel=\"author\"");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("entry entry-content");
                            if (startIndex == -1) startIndex = sourceCode.IndexOf("entry-content");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("<!--end .entry");
                            if (endIndex == -1) endIndex = sourceCode.IndexOf("<div id=\"sidebar\">");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<a"))
                            {
                                endIndex = story.IndexOf("<a");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<span"))
                            {
                                endIndex = story.IndexOf("<span");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<ins"))
                            {
                                endIndex = story.IndexOf("<ins");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<iframe"))
                            {
                                endIndex = story.IndexOf("<iframe");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<td"))
                            {
                                endIndex = story.IndexOf("<td");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<table"))
                            {
                                endIndex = story.IndexOf("<table");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<font"))
                            {
                                endIndex = story.IndexOf("<font");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<style>"))
                            {
                                endIndex = story.IndexOf("<style>");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</style>") + 8;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</style>", "");
                            story = story.Replace("</span>", "");
                            story = story.Replace("</script>", "");
                            story = story.Replace("</a>", "");
                            story = story.Replace("<br />", "");
                            story = story.Replace("</ul>", "");
                            story = story.Replace("</li>", "");
                            story = story.Replace("<tr>", "");
                            story = story.Replace("</tr>", "");
                            story = story.Replace("<td>", "");
                            story = story.Replace("</td>", "");
                            story = story.Replace("<p>", "");
                            story = story.Replace("<p", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</ins>", "");
                            story = story.Replace("</div>", "");
                            story = story.Replace("</font>", "");
                            story = story.Replace("</i>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<i>", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&lsquo;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&#8216;", "'");
                            story = story.Replace("&#8217;", "'");
                            story = story.Replace("&#8220;", "\"");
                            story = story.Replace("&#8221;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("\r", "");
                            story = story.Replace("&mdash;", " - ");
                            story = story.Replace("</iframe>", "");
                            story = story.Replace("(adsbygoogle = window.adsbygoogle || []).push({});", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region WKRB News
                    if (u.Website.Trim().ToLower() == "wkrb news")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);
                            if (sourceCode.ToLower() != "invalid")
                            {

                                /* Find TITLE */
                                int startIndex = sourceCode.IndexOf("<title>");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("|");
                                if (endIndex > 50 || endIndex == -1) endIndex = sourceCode.IndexOf("</");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&quot;", "\"");

                                /* Author */
                                startIndex = sourceCode.IndexOf("rel=\"author\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                string author = sourceCode.Substring(0, endIndex).Trim();

                                /* Story */
                                startIndex = sourceCode.IndexOf("class=\"entry\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("<p style=\"clear:both\">");
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</script>") + 9;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf("</div>") + 6;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&#39;", "'");
                                story = story.Replace("&#8216;", "'");
                                story = story.Replace("&#8217;", "'");
                                story = story.Replace("&#8220;", "\"");
                                story = story.Replace("&#8221;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();
                            }
                            else
                            {
                                u.Author = "Not Found";
                                u.Story = "Page Not Found";
                            }

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region Yahoo!7 News
                    if (u.Website.Trim().ToLower().Contains("yahoo!7"))
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            if (u.Link.Contains("video/"))
                            {
                                u.Author = "Undefined";

                                int startIndex = sourceCode.IndexOf("class=\"video-desc\"");
                                if (startIndex != -1)
                                {
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    startIndex = sourceCode.IndexOf(">") + 1;
                                    sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                    int endIndex = sourceCode.IndexOf("</");
                                    u.Story = sourceCode.Substring(0, endIndex).Trim();
                                }
                                else u.Story = "This is a video file.";
                            }
                            else if (u.Link.Contains("/q?"))
                            {
                                u.Author = "Undefined";
                                u.Story = "This is a graph file.";
                            }
                            else
                            {
                                /* Find TITLE */
                                int startIndex = sourceCode.IndexOf("<title>") + 7;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("</");
                                string title = sourceCode.Substring(0, endIndex).Trim();
                                title = title.Replace("&#8217;", "'");

                                /* Author */
                                string author = "Undefined";

                                /* Story */
                                startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                                if (startIndex == -1) startIndex = sourceCode.IndexOf("class=\"body-slot-mod\"");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("google_ad_section_end");
                                if (endIndex == -1) endIndex = sourceCode.IndexOf("<!--");
                                string story = sourceCode.Substring(0, endIndex).Trim();
                                while (story.Contains("<a"))
                                {
                                    endIndex = story.IndexOf("<a");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<p"))
                                {
                                    endIndex = story.IndexOf("<p");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 2, story.Length - (endIndex + 2));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ul"))
                                {
                                    endIndex = story.IndexOf("<ul");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<li"))
                                {
                                    endIndex = story.IndexOf("<li");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<img"))
                                {
                                    endIndex = story.IndexOf("<img");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<span"))
                                {
                                    endIndex = story.IndexOf("<span");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 5, story.Length - (endIndex + 5));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<script"))
                                {
                                    endIndex = story.IndexOf("<script");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 6, story.Length - (endIndex + 6));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<ins"))
                                {
                                    endIndex = story.IndexOf("<ins");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<div"))
                                {
                                    endIndex = story.IndexOf("<div");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                    startIndex = story.IndexOf(">") + 1;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<style>"))
                                {
                                    endIndex = story.IndexOf("<style>");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                    startIndex = story.IndexOf("</style>") + 8;
                                    string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                while (story.Contains("<!--"))
                                {
                                    endIndex = story.IndexOf("<!--");
                                    string firstPart = story.Substring(0, endIndex);
                                    story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                    startIndex = story.IndexOf("-->") + 3;
                                    string secondPart = "";
                                    if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                    story = firstPart + "\n" + secondPart;
                                }
                                story = story.Replace("</style>", " ");
                                story = story.Replace("</span>", " ");
                                story = story.Replace("</script>", " ");
                                story = story.Replace("</a>", " ");
                                story = story.Replace("<br />", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("</tr>", " ");
                                story = story.Replace("</td>", " ");
                                story = story.Replace("</ul>", " ");
                                story = story.Replace("</li>", " ");
                                story = story.Replace("<p>", "");
                                story = story.Replace("</p>", " ");
                                story = story.Replace("<P>", "");
                                story = story.Replace("</P>", " ");
                                story = story.Replace("</ins>", " ");
                                story = story.Replace("</div>", " ");
                                story = story.Replace("&nbsp;", " ");
                                story = story.Replace("&lsquo;", "'");
                                story = story.Replace("&rsquo;", "'");
                                story = story.Replace("&ldquo;", "\"");
                                story = story.Replace("&rdquo;", "\"");
                                story = story.Replace("&quot;", "\"");
                                story = story.Replace("&amp;", "&");
                                story = story.Replace("\n", "");
                                story = story.Replace("\t", "");
                                story = story.Replace("<b>", "");
                                story = story.Replace("</b>", "");
                                story = story.Replace("<strong>", "");
                                story = story.Replace("</strong>", "");

                                if (u.Title != title) u.Title = title;
                                u.Author = author;
                                u.Story = story.Trim();
                            }
                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region ZDNet
                    if (u.Website.Trim().ToLower() == "zdnet")
                    {
                        try
                        {
                            string sourceCode = WorkerClasses.getSourceCode(u.Link);

                            /* Find TITLE */
                            int startIndex = sourceCode.IndexOf("<title>");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("|");
                            if (endIndex > 40 || endIndex == -1) endIndex = sourceCode.IndexOf("</");
                            string title = sourceCode.Substring(0, endIndex).Trim();
                            title = title.Replace("&#8217;", "'");

                            /* Author */
                            string author = "Undefined";
                            startIndex = sourceCode.IndexOf("rel='author'");
                            if (startIndex != -1)
                            {
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</");
                                author = sourceCode.Substring(0, endIndex).Trim();
                            }

                            /* Story */
                            startIndex = sourceCode.IndexOf("itemprop=\"articleBody\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("\"relatedTopics\"");
                            string story = sourceCode.Substring(0, endIndex).Trim();
                            while (story.Contains("<script"))
                            {
                                endIndex = story.IndexOf("<script");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 7, story.Length - (endIndex + 7));
                                startIndex = story.IndexOf("</script>") + 9;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<img"))
                            {
                                endIndex = story.IndexOf("<img");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf(">") + 1;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<div"))
                            {
                                endIndex = story.IndexOf("<div");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 4, story.Length - (endIndex + 4));
                                startIndex = story.IndexOf("</div>") + 6;
                                string secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            while (story.Contains("<!--"))
                            {
                                endIndex = story.IndexOf("<!--");
                                string firstPart = story.Substring(0, endIndex);
                                story = story.Substring(endIndex + 3, story.Length - (endIndex + 3));
                                startIndex = story.IndexOf("-->") + 3;
                                string secondPart = "";
                                if (story.IndexOf("-->") != -1) secondPart = story.Substring(startIndex, story.Length - startIndex);
                                story = firstPart + "\n" + secondPart;
                            }
                            story = story.Replace("</span>", " ");
                            story = story.Replace("</script>", " ");
                            story = story.Replace("</ul>", " ");
                            story = story.Replace("</li>", " ");
                            story = story.Replace("</tr>", " ");
                            story = story.Replace("</td>", " ");
                            story = story.Replace("<p>", "");
                            story = story.Replace("</p>", " ");
                            story = story.Replace("<P>", "");
                            story = story.Replace("</P>", " ");
                            story = story.Replace("</div>", " ");
                            story = story.Replace("&nbsp;", " ");
                            story = story.Replace("&#39;", "'");
                            story = story.Replace("&rsquo;", "'");
                            story = story.Replace("&ldquo;", "\"");
                            story = story.Replace("&rdquo;", "\"");
                            story = story.Replace("&quot;", "\"");
                            story = story.Replace("&amp;", "&");
                            story = story.Replace("\n", "");
                            story = story.Replace("\t", "");
                            story = story.Replace("<b>", "");
                            story = story.Replace("</b>", "");
                            story = story.Replace("<strong>", "");
                            story = story.Replace("</strong>", "");

                            if (u.Title != title) u.Title = title;
                            u.Author = author;
                            u.Story = story.Trim();

                            db.SaveChanges();
                            saved++;
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                            continue;
                        }
                    }
                    #endregion

                    #region @@@ Negative Words and Positive Words
                    // Retreive a source code from a webpage
                    // e.g. http://www3.nd.edu/~mcdonald/Word_Lists.html
                    try
                    {
                        string word = ""; int posCount = 0; int negCount = 0;
                        
                        if (u.Story != null && u.Story != "")
                        {
                            if (u.PosWords == null && u.NegWords == null)
                            {
                                string content = u.Story;
                                string[] contentSplit = content.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
                                // Select a word (read by line) in the list
                                while (positiveCode.IndexOf("\r") != -1)
                                {
                                    int endIndex = positiveCode.IndexOf("\r");
                                    word = positiveCode.Substring(0, endIndex);
                                    positiveCode = positiveCode.Substring(endIndex + 1, positiveCode.Length - endIndex - 1);
                                    if (content.Contains(word))
                                    {
                                        var matchQuery = from words in contentSplit
                                                         where words.ToLowerInvariant() == word.ToLowerInvariant()
                                                         select words;
                                        posCount += matchQuery.Count();
                                    }
                                }
                                while (negativeCode.IndexOf("\r") != -1)
                                {
                                    int endIndex = negativeCode.IndexOf("\r");
                                    word = negativeCode.Substring(0, endIndex);
                                    negativeCode = negativeCode.Substring(endIndex + 1, negativeCode.Length - endIndex - 1);
                                    if (content.Contains(word))
                                    {
                                        var matchQuery = from words in contentSplit
                                                         where words.ToLowerInvariant() == word.ToLowerInvariant()
                                                         select words;
                                        negCount += matchQuery.Count();
                                    }
                                }

                                u.PosWords = posCount;
                                u.NegWords = negCount;
                                u.Length_of_Post = Regex.Matches(u.Story, @"[\S]+").Count;  
                                db.SaveChanges();
                                updatedData++;

                                positiveCode = positiveCodeCopy;
                                negativeCode = negativeCodeCopy;
                                posCount = 0;
                                negCount = 0;
                            }
                            else
                                wordsDuplicates++;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("UriFormatException"))
                        {
                            errorBox.Items.Add("Invalid URL!");
                            MessageBox.Show("Invalid URL!");
                        }
                        else
                        {
                            errorBox.Items.Add(ex.Message);
                        }
                    }
                    #endregion
                }
                else
                {
                    duplicates++;
                }
            }
            string filledPercent = ((double)(duplicates + saved) * 100 / articlesList.Count).ToString("#.##");
            errorBox.Items.Add("\n[" + DateTime.Now + "] Task Ended.\n" + saved + " Article(s) Saved, " + duplicates + " already exist in database. " + filledPercent + "% news include data.");
            errorBox.Items.Add("\n[" + DateTime.Now + "] Task Ended.\n" + updatedData + " Pos/Neg(words) Saved, " + wordsDuplicates + " already exist in database.");
            errorBox.Items.Add(" ");
        }

        /* For News which cannot be retrieved by Web Crawling */
        private void button3_Click(object sender, EventArgs e)
        {
            var db = new FinanceCrawlerEntities();
            int saved = 0;
            int duplicates = 0;
            int updatedData = 0;
            int wordsDuplicates = 0;
            var articlesList = (from f in db.GoogleFinance_News
                                where f.Story == ""
                                orderby f.Website
                                select f).ToList();

            // Positive Words
            string positiveWordLink = "http://www3.nd.edu/~mcdonald/Data/Finance_Word_Lists/LoughranMcDonald_Positive.csv";
            string positiveCode = WorkerClasses.getSourceCode(positiveWordLink).ToLower().Replace("\n", "");
            if (positiveCode == "invalid") throw new UriFormatException();
            string positiveCodeCopy = positiveCode;

            // Negative Words
            string negativeWordLink = "http://www3.nd.edu/~mcdonald/Data/Finance_Word_Lists/LoughranMcDonald_Negative.csv";
            string negativeCode = WorkerClasses.getSourceCode(negativeWordLink).ToLower().Replace("\n", "");
            if (negativeCode == "invalid") throw new UriFormatException();
            string negativeCodeCopy = negativeCode;

            errorBox.Items.Add("Pushing data starts! Processing now...");
            errorBox.Items.Add("---------------------------------------------------------");

            DialogResult result = openFileDialog1.ShowDialog();
            string story = "";
            if (result == DialogResult.OK) // Test result.
            {
                string file = openFileDialog1.FileName;
                try
                {
                    story = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    errorBox.Items.Add("ERROR: Not appropriate file selected!");
                }
            }

            string title = textBox3.Text;
            string author = textBox4.Text;

            if (title == "") errorBox.Items.Add("Alert: Please enter TITLE.");
            if (author == "") author = "Undefined";

            if (title != "" && story != "")
            {
                foreach (var u in articlesList)
                {
                    try
                    {
                        if (title.Contains(u.Title.Replace("...", "")))
                        {
                            if (u.Story == null || u.Story == "")
                            {
                                u.Author = author;
                                u.Story = story;

                                db.SaveChanges();
                                saved++;

                                #region @@@ Negative Words and Positive Words
                                // Retreive a source code from a webpage
                                // e.g. http://www3.nd.edu/~mcdonald/Word_Lists.html
                                try
                                {
                                    string word = ""; int posCount = 0; int negCount = 0;

                                    if (u.Story != null && u.Story != "")
                                    {
                                        if (u.PosWords == null && u.NegWords == null)
                                        {
                                            string content = u.Story;
                                            string[] contentSplit = content.Split(new char[] { '.', '?', '!', ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
                                            // Select a word (read by line) in the list
                                            while (positiveCode.IndexOf("\r") != -1)
                                            {
                                                int endIndex = positiveCode.IndexOf("\r");
                                                word = positiveCode.Substring(0, endIndex);
                                                positiveCode = positiveCode.Substring(endIndex + 1, positiveCode.Length - endIndex - 1);
                                                if (content.Contains(word))
                                                {
                                                    var matchQuery = from words in contentSplit
                                                                     where words.ToLowerInvariant() == word.ToLowerInvariant()
                                                                     select words;
                                                    posCount += matchQuery.Count();
                                                }
                                            }
                                            while (negativeCode.IndexOf("\r") != -1)
                                            {
                                                int endIndex = negativeCode.IndexOf("\r");
                                                word = negativeCode.Substring(0, endIndex);
                                                negativeCode = negativeCode.Substring(endIndex + 1, negativeCode.Length - endIndex - 1);
                                                if (content.Contains(word))
                                                {
                                                    var matchQuery = from words in contentSplit
                                                                     where words.ToLowerInvariant() == word.ToLowerInvariant()
                                                                     select words;
                                                    negCount += matchQuery.Count();
                                                }
                                            }

                                            u.PosWords = posCount;
                                            u.NegWords = negCount;
                                            u.Length_of_Post = Regex.Matches(u.Story, @"[\S]+").Count;
                                            db.SaveChanges();
                                            updatedData++;

                                            positiveCode = positiveCodeCopy;
                                            negativeCode = negativeCodeCopy;
                                            posCount = 0;
                                            negCount = 0;
                                        }
                                        else
                                            wordsDuplicates++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    if (ex.Message.Contains("UriFormatException"))
                                    {
                                        errorBox.Items.Add("Invalid URL!");
                                        MessageBox.Show("Invalid URL!");
                                    }
                                    else
                                    {
                                        errorBox.Items.Add(ex.Message);
                                    }
                                }
                                #endregion
                            }
                            else
                            {
                                duplicates++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errorBox.Items.Add(u.Website + " failed because of an error: " + ex);
                        continue;
                    }
                }
                string filledPercent = ((double)(duplicates + saved) * 100 / articlesList.Count).ToString("#.##");
                if (duplicates == 0 && saved == 0)
                {
                    errorBox.Items.Add("\n[" + DateTime.Now + "] No matching result found in the database. Please recheck the Title.");
                }
                errorBox.Items.Add("\n[" + DateTime.Now + "] Task Ended.\n" + saved + " Article(s) Saved, " + duplicates + " already exist in database. " + filledPercent + "% news include data.");
                errorBox.Items.Add("\n[" + DateTime.Now + "] Task Ended.\n" + updatedData + " Pos/Neg(words) Saved, " + wordsDuplicates + " already exist in database.");
                errorBox.Items.Add(" ");
            }
        }

        /* Finance Data */
        private void button4_Click(object sender, EventArgs e)
        {
            var db = new FinanceCrawlerEntities();
            int duplicates = 0;
            int newData = 0;

            // Retreive source code from a webpage
            string url = textBox1.Text;
            if (url != null && url.Trim() != "")
            {
                try
                {
                    string sourceCode = WorkerClasses.getSourceCode(url);
                    if (sourceCode == "invalid") throw new UriFormatException();
                    if (!url.Contains("www.google.com/finance?q=")) throw new UriFormatException();

                    /* Group */
                    string groupWord = textBox2.Text;
                    if (groupWord == "")
                        groupWord = WorkerClasses.getGroupWord(url).ToUpper();

                    #region GOOGLE FINANCE DATA RESULT ONLY ALLOWED
                    try
                    {
                        int startIndex = sourceCode.IndexOf("id=companyheader");  // Finance data starts from here
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);

                        /* Name */
                        startIndex = sourceCode.IndexOf("<h");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        int endIndex = sourceCode.IndexOf("</");
                        string name = sourceCode.Substring(0, endIndex);
                        name = name.Replace("%3F", "?");
                        name = name.Replace("%3D", "=");
                        name = name.Replace("%26", "&");
                        name = name.Replace("&nbsp;", "");

                        /* Price */
                        startIndex = sourceCode.IndexOf("class=\"pr\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string priceStr = sourceCode.Substring(0, endIndex);
                        priceStr = priceStr.Replace("&nbsp;", " ");
                        priceStr = priceStr.Replace("&#39;", "'");
                        priceStr = priceStr.Replace("&quot;", "\"");
                        priceStr = priceStr.Replace("&amp;", "&");
                        while (priceStr.Contains("<span"))
                        {
                            endIndex = priceStr.IndexOf("<span");
                            string firstPart = priceStr.Substring(0, endIndex);
                            priceStr = priceStr.Substring(endIndex + 5, priceStr.Length - (endIndex + 5));
                            startIndex = priceStr.IndexOf(">") + 1;
                            string secondPart = priceStr.Substring(startIndex, priceStr.Length - startIndex);
                            priceStr = firstPart + "\n" + secondPart;
                        }
                        Decimal price = Convert.ToDecimal(priceStr);

                        /* Date */
                        DateTime createdDate = DateTime.Now;

                        /* Range */  
                        startIndex = sourceCode.IndexOf("data-snapfield=\"range\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string range = sourceCode.Substring(0, endIndex);
                        int midIndex = range.IndexOf("-");
                        string rangeFromStr = range.Substring(0, midIndex);
                        string rangeToStr = range.Substring(midIndex + 1);
                        Decimal rangeFrom = Convert.ToDecimal(rangeFromStr);
                        Decimal rangeTo = Convert.ToDecimal(rangeToStr);

                        /* 52 week */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"range_52week\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        range = sourceCode.Substring(0, endIndex);
                        midIndex = range.IndexOf("-");
                        rangeFromStr = range.Substring(0, midIndex);
                        rangeToStr = range.Substring(midIndex + 1);
                        Decimal fiftytwoWeekFrom = Convert.ToDecimal(rangeFromStr);
                        Decimal fiftytwoWeekTo = Convert.ToDecimal(rangeToStr);

                        /* Open */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"open\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string openStr = sourceCode.Substring(0, endIndex);
                        Decimal open = Convert.ToDecimal(openStr);

                        /* Vol (M), Avg (M) */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"vol_and_avg\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string volavg = sourceCode.Substring(0, endIndex);
                        midIndex = volavg.IndexOf("/");
                        Decimal vol, avg = 0;
                        if (midIndex != -1)
                        {
                            string volStr = volavg.Substring(0, midIndex);
                            string avgStr = volavg.Substring(midIndex + 1);

                            if (volStr.ToLower().Contains("m"))
                            {
                                volStr = volStr.ToLower().Replace("m", "");
                                vol = Convert.ToDecimal(volStr);
                            }
                            else if (volStr.ToLower().Contains("b"))
                            {
                                volStr = volStr.ToLower().Replace("b", "");
                                vol = Convert.ToDecimal(volStr) * 1000000;
                            }
                            else
                            {
                                vol = Convert.ToDecimal(volStr); ;
                                vol /= 1000000;
                            }

                            if (avgStr.ToLower().Contains("m"))
                            {
                                avgStr = avgStr.ToLower().Replace("m", "");
                                avg = Convert.ToDecimal(avgStr);
                            }
                            else if (avgStr.ToLower().Contains("b"))
                            {
                                avgStr = avgStr.ToLower().Replace("b", "");
                                avg = Convert.ToDecimal(avgStr) * 1000000;
                            }
                            else
                            {
                                avg = Convert.ToDecimal(avgStr); ;
                                avg /= 1000000;
                            }
                        }
                        else
                        {
                            string volStr = volavg;

                            if (volStr.ToLower().Contains("m"))
                            {
                                volStr = volStr.ToLower().Replace("m", "");
                                vol = Convert.ToDecimal(volStr);
                            }
                            else if (volStr.ToLower().Contains("b"))
                            {
                                volStr = volStr.ToLower().Replace("b", "");
                                vol = Convert.ToDecimal(volStr) * 1000000;
                            }
                            else
                            {
                                vol = Convert.ToDecimal(volStr); ;
                                vol /= 1000000;
                            }
                        }

                        /* Mkt Cap (B) */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"market_cap\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string mktCapStr = sourceCode.Substring(0, endIndex);
                        Decimal mktCap = 0;
                        if (!mktCapStr.Contains("&nbsp;-"))
                        {
                            if (mktCapStr.ToLower().Contains("b"))
                            {
                                mktCapStr = mktCapStr.ToLower().Replace("b", "");
                                mktCap = Convert.ToDecimal(mktCapStr);
                            }
                            else if (mktCapStr.ToLower().Contains("m"))
                            {
                                mktCapStr = mktCapStr.ToLower().Replace("m", "");
                                mktCap = Convert.ToDecimal(mktCapStr);
                                mktCap /= 1000;
                            }
                            else
                            {
                                mktCap = Convert.ToDecimal(mktCapStr);
                                mktCap /= 1000000000;
                            }
                        }

                        /* P/E */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"pe_ratio\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string peStr = sourceCode.Substring(0, endIndex);
                        if (peStr.Contains("&nbsp;-")) peStr = "0";
                        Decimal pe = Convert.ToDecimal(peStr);

                        /* Div, yield */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"latest_dividend-dividend_yield\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        range = sourceCode.Substring(0, endIndex);
                        string divStr, yieldStr = "";
                        midIndex = range.IndexOf("/");
                        if (midIndex != -1)
                        {
                            divStr = range.Substring(0, midIndex);
                            if (divStr.Contains("-")) divStr = "0";
                            yieldStr = range.Substring(midIndex + 1);
                            if (yieldStr.Contains("-")) yieldStr = "0";
                        }
                        else
                        {
                            divStr = "0";
                            yieldStr = "0";
                        }
                        Decimal div = Convert.ToDecimal(divStr);
                        Decimal yield = Convert.ToDecimal(yieldStr);

                        /* EPS */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"eps\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string epsStr = sourceCode.Substring(0, endIndex);
                        if (epsStr.Contains("&nbsp;-")) epsStr = "0";
                        Decimal eps = Convert.ToDecimal(epsStr);

                        /* Shares */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"shares\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string sharesStr = sourceCode.Substring(0, endIndex);
                        Decimal shares = 0;
                        if (sharesStr.ToLower().Contains("b"))
                        {
                            sharesStr = sharesStr.ToLower().Replace("b", "");
                            shares = Convert.ToDecimal(sharesStr);
                        }
                        else if (sharesStr.ToLower().Contains("m"))
                        {
                            sharesStr = sharesStr.ToLower().Replace("m", "");
                            shares = Convert.ToDecimal(sharesStr);
                            shares /= 1000;
                        }
                        else
                        {
                            if (sharesStr.Contains("&nbsp;-")) sharesStr = "0";
                            else
                            {
                                shares = Convert.ToDecimal(sharesStr);
                                shares /= 1000000000;
                            }
                        }

                        /* Beta */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"beta\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string betaStr = sourceCode.Substring(0, endIndex).Trim();
                        betaStr = betaStr.Replace("&nbsp;", "");

                        /* S&P/ASP 200  Inst. own */
                        startIndex = sourceCode.IndexOf("data-snapfield=\"inst_own\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf("class=\"val\"");
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        startIndex = sourceCode.IndexOf(">") + 1;
                        sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                        endIndex = sourceCode.IndexOf("</");
                        string instOwnStr = sourceCode.Substring(0, endIndex);
                        instOwnStr = instOwnStr.Replace("%", "");
                        if (instOwnStr.Contains("&nbsp;-")) instOwnStr = "0";
                        Int64 instOwn = Convert.ToInt64(instOwnStr);

                        if (!db.GoogleFinance_Data.Any(f => f.Date == createdDate))
                        {
                            db.GoogleFinance_Data.Add(new GoogleFinance_Data
                            {
                                Name = name,
                                Price = price,
                                Date = createdDate,
                                Group = groupWord,
                                Avg_M_ = avg,
                                Beta = betaStr,
                                Div = div,
                                EPS = eps,
                                Open = open,
                                P_E = pe,
                                S_P_ASX_200 = instOwn,
                                Yield = yield,
                                Vol_M_ = vol,
                                Shares_B_ = shares,
                                Mkt_Cap_B_ = mktCap,
                                Range_From = rangeFrom,
                                Range_To = rangeTo,
                                C52_Weeks_From = fiftytwoWeekFrom,
                                C52_Weeks_To = fiftytwoWeekTo
                            });
                            db.SaveChanges();
                            newData++;
                        }
                        else
                        {
                            duplicates++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorBox.Items.Add("Failed because of an error: " + ex);
                    }
                    
                    errorBox.Items.Add("\n[" + DateTime.Now + "] Task Ended.\n" + newData + " saved and " + duplicates + " duplicates Found.");
                    #endregion
                }
                catch (Exception)
                {
                    errorBox.Items.Add("Invalid URL!");
                    MessageBox.Show("Invalid URL!");
                    textBox1.Text = "";
                }
            }
            else
            {
                errorBox.Items.Add("Please enter URL.");
                MessageBox.Show("Please enter URL.");
            }
        }

        /* Google Finance News By Text File */
        private void button5_Click(object sender, EventArgs e)
        {
            var db = new FinanceCrawlerEntities();
            int duplicates = 0;
            int newData = 0;

            errorBox.Items.Add("[" + DateTime.Now + "] Google Finance News by text file begins! Please wait for a few seconds.");
            DialogResult result = openFileDialog1.ShowDialog();
            string links = "";
            if (result == DialogResult.OK) // Test result.
            {
                string file = openFileDialog1.FileName;
                try
                {
                    links = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    errorBox.Items.Add("ERROR: Not appropriate file selected!");
                }
            }

            // Retreive source code from a webpage
            StringReader strReader = new StringReader(links);
            while (true)
            {
                string url = strReader.ReadLine();
                if (url != null && url.Trim() != "")
                {
                    try
                    {
                        string sourceCode = WorkerClasses.getSourceCode(url);
                        if (sourceCode == "invalid") throw new UriFormatException();

                        /* Group */
                        string groupWord = textBox2.Text;
                        if (groupWord == "")
                            groupWord = WorkerClasses.getGroupWord(url);

                        #region GOOGLE FINANCE NEWS RESULT ONLY ALLOWED
                        while (sourceCode.IndexOf("g-section news") > -1)
                        {
                            try
                            {
                                int startIndex = sourceCode.IndexOf("g-section news");  // News article's information starts from here
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);

                                /* Article's Page Link */
                                startIndex = sourceCode.IndexOf("span class=name");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("a href=");
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf("\"") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                int endIndex = sourceCode.IndexOf("\"");
                                string pageLink = sourceCode.Substring(0, endIndex);
                                pageLink = pageLink.Replace("%3F", "?");
                                pageLink = pageLink.Replace("%3D", "=");
                                pageLink = pageLink.Replace("%26", "&");
                                if (pageLink.Contains("url="))
                                {
                                    startIndex = pageLink.IndexOf("url=") + 4;
                                    pageLink = pageLink.Substring(startIndex, pageLink.Length - startIndex);
                                    if (pageLink.Contains("&amp;"))
                                    {
                                        endIndex = pageLink.IndexOf("&amp;");
                                        pageLink = pageLink.Substring(0, endIndex);
                                    }
                                }
                                //MessageBox.Show("Article Link is: " + pageLink);

                                /* Ariticle's Title */
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</a>");
                                string pageTitle = sourceCode.Substring(0, endIndex);
                                pageTitle = pageTitle.Replace("&nbsp;", " ");
                                pageTitle = pageTitle.Replace("&#39;", "'");
                                pageTitle = pageTitle.Replace("&quot;", "\"");
                                pageTitle = pageTitle.Replace("&amp;", "&");
                                //MessageBox.Show("Article Title is: " + pageTitle);

                                /* Ariticle's source website name */
                                startIndex = sourceCode.IndexOf("<span class=src") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</span>");
                                string webpage = sourceCode.Substring(0, endIndex);
                                //MessageBox.Show("Article source webpage is: " + webpage);

                                /* Ariticle's created date */
                                startIndex = sourceCode.IndexOf("<span class=date") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                startIndex = sourceCode.IndexOf(">") + 1;
                                sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                                endIndex = sourceCode.IndexOf("</span>");
                                string createdDateStr = sourceCode.Substring(0, endIndex);
                                DateTime createdDate = DateTime.Now;
                                if (!createdDateStr.Contains("ago"))
                                    createdDate = Convert.ToDateTime(createdDateStr);
                                else
                                {
                                    if (createdDateStr.Contains("hour"))
                                    {
                                        createdDateStr = createdDateStr.Replace(" hours ago", "");
                                        createdDateStr = createdDateStr.Replace(" hour ago", "");
                                        createdDate = DateTime.Now.AddHours(-Int32.Parse(createdDateStr));
                                    }
                                    else if (createdDateStr.Contains("minute"))
                                    {
                                        createdDateStr = createdDateStr.Replace(" minutes ago", "");
                                        createdDateStr = createdDateStr.Replace(" minute ago", "");
                                        createdDate = DateTime.Now.AddMinutes(-Int32.Parse(createdDateStr));
                                    }
                                }

                                if (!db.GoogleFinance_News.Any(f => f.Link == pageLink))
                                {
                                    db.GoogleFinance_News.Add(new GoogleFinance_News
                                    {
                                        Group = groupWord,
                                        Title = pageTitle,
                                        Website = webpage,
                                        Date = createdDate,
                                        Link = pageLink
                                    });
                                    db.SaveChanges();
                                    newData++;
                                }
                                else
                                {
                                    duplicates++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errorBox.Items.Add(groupWord + " failed because of an error: " + ex);
                                continue;
                            }
                        }
                        #endregion
                    }
                    catch (Exception)
                    {
                        errorBox.Items.Add("Invalid URL!");
                        MessageBox.Show("Invalid URL!");
                        textBox1.Text = "";
                    }
                }
                else
                {
                    PrintCompleted();
                    break;
                }
            }
            errorBox.Items.Add("\n[" + DateTime.Now + "] Task Ended.\n" + newData + " saved and " + duplicates + " duplicates Found.");
        }

        /* Finance Data By Text File */
        private void button6_Click(object sender, EventArgs e)
        {
            var db = new FinanceCrawlerEntities();
            int duplicates = 0;
            int newData = 0;

            errorBox.Items.Add("[" + DateTime.Now + "] Google Finance Data by text file begins! Please wait for a few seconds.");
            DialogResult result = openFileDialog1.ShowDialog();
            string links = "";
            if (result == DialogResult.OK) // Test result.
            {
                string file = openFileDialog1.FileName;
                try
                {
                    links = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    errorBox.Items.Add("ERROR: Not appropriate file selected!");
                }
            }

            // Retreive source code from a webpage
            StringReader strReader = new StringReader(links);
            while (true)
            {
                string url = strReader.ReadLine();
                if (url != null && url.Trim() != "")
                {
                    #region SAME AS "FINANCE DATA" BUTTON (button 4)
                    try
                    {
                        string sourceCode = WorkerClasses.getSourceCode(url);
                        if (sourceCode == "invalid") throw new UriFormatException();
                        if (!url.Contains("www.google.com/finance?q=")) throw new UriFormatException();

                        /* Group */
                        string groupWord = textBox2.Text;
                        if (groupWord == "")
                            groupWord = WorkerClasses.getGroupWord(url).ToUpper();

                        #region GOOGLE FINANCE DATA RESULT ONLY ALLOWED
                        try
                        {
                            int startIndex = sourceCode.IndexOf("id=companyheader");  // Finance data starts from here
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);

                            /* Name */
                            startIndex = sourceCode.IndexOf("<h");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            int endIndex = sourceCode.IndexOf("</");
                            string name = sourceCode.Substring(0, endIndex);
                            name = name.Replace("%3F", "?");
                            name = name.Replace("%3D", "=");
                            name = name.Replace("%26", "&");
                            name = name.Replace("&nbsp;", "");

                            /* Price */
                            startIndex = sourceCode.IndexOf("class=\"pr\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string priceStr = sourceCode.Substring(0, endIndex);
                            priceStr = priceStr.Replace("&nbsp;", " ");
                            priceStr = priceStr.Replace("&#39;", "'");
                            priceStr = priceStr.Replace("&quot;", "\"");
                            priceStr = priceStr.Replace("&amp;", "&");
                            while (priceStr.Contains("<span"))
                            {
                                endIndex = priceStr.IndexOf("<span");
                                string firstPart = priceStr.Substring(0, endIndex);
                                priceStr = priceStr.Substring(endIndex + 5, priceStr.Length - (endIndex + 5));
                                startIndex = priceStr.IndexOf(">") + 1;
                                string secondPart = priceStr.Substring(startIndex, priceStr.Length - startIndex);
                                priceStr = firstPart + "\n" + secondPart;
                            }
                            Decimal price = Convert.ToDecimal(priceStr);

                            /* Date */
                            DateTime createdDate = DateTime.Now;

                            /* Range */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"range\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string range = sourceCode.Substring(0, endIndex);
                            int midIndex = range.IndexOf("-");
                            string rangeFromStr = range.Substring(0, midIndex);
                            string rangeToStr = range.Substring(midIndex + 1);
                            Decimal rangeFrom = Convert.ToDecimal(rangeFromStr);
                            Decimal rangeTo = Convert.ToDecimal(rangeToStr);

                            /* 52 week */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"range_52week\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            range = sourceCode.Substring(0, endIndex);
                            midIndex = range.IndexOf("-");
                            rangeFromStr = range.Substring(0, midIndex);
                            rangeToStr = range.Substring(midIndex + 1);
                            Decimal fiftytwoWeekFrom = Convert.ToDecimal(rangeFromStr);
                            Decimal fiftytwoWeekTo = Convert.ToDecimal(rangeToStr);

                            /* Open */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"open\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string openStr = sourceCode.Substring(0, endIndex);
                            Decimal open = Convert.ToDecimal(openStr);

                            /* Vol (M), Avg (M) */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"vol_and_avg\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string volavg = sourceCode.Substring(0, endIndex);
                            midIndex = volavg.IndexOf("/");
                            Decimal vol, avg = 0;
                            if (midIndex != -1)
                            {
                                string volStr = volavg.Substring(0, midIndex);
                                string avgStr = volavg.Substring(midIndex + 1);

                                if (volStr.ToLower().Contains("m"))
                                {
                                    volStr = volStr.ToLower().Replace("m", "");
                                    vol = Convert.ToDecimal(volStr);
                                }
                                else if (volStr.ToLower().Contains("b"))
                                {
                                    volStr = volStr.ToLower().Replace("b", "");
                                    vol = Convert.ToDecimal(volStr) * 1000000;
                                }
                                else
                                {
                                    vol = Convert.ToDecimal(volStr); ;
                                    vol /= 1000000;
                                }

                                if (avgStr.ToLower().Contains("m"))
                                {
                                    avgStr = avgStr.ToLower().Replace("m", "");
                                    avg = Convert.ToDecimal(avgStr);
                                }
                                else if (avgStr.ToLower().Contains("b"))
                                {
                                    avgStr = avgStr.ToLower().Replace("b", "");
                                    avg = Convert.ToDecimal(avgStr) * 1000000;
                                }
                                else
                                {
                                    avg = Convert.ToDecimal(avgStr); ;
                                    avg /= 1000000;
                                }
                            }
                            else
                            {
                                string volStr = volavg;

                                if (volStr.ToLower().Contains("m"))
                                {
                                    volStr = volStr.ToLower().Replace("m", "");
                                    vol = Convert.ToDecimal(volStr);
                                }
                                else if (volStr.ToLower().Contains("b"))
                                {
                                    volStr = volStr.ToLower().Replace("b", "");
                                    vol = Convert.ToDecimal(volStr) * 1000000;
                                }
                                else
                                {
                                    vol = Convert.ToDecimal(volStr); ;
                                    vol /= 1000000;
                                }
                            }

                            /* Mkt Cap (B) */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"market_cap\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string mktCapStr = sourceCode.Substring(0, endIndex);
                            Decimal mktCap = 0;
                            if (!mktCapStr.Contains("&nbsp;-"))
                            {
                                if (mktCapStr.ToLower().Contains("b"))
                                {
                                    mktCapStr = mktCapStr.ToLower().Replace("b", "");
                                    mktCap = Convert.ToDecimal(mktCapStr);
                                }
                                else if (mktCapStr.ToLower().Contains("m"))
                                {
                                    mktCapStr = mktCapStr.ToLower().Replace("m", "");
                                    mktCap = Convert.ToDecimal(mktCapStr);
                                    mktCap /= 1000;
                                }
                                else
                                {
                                    mktCap = Convert.ToDecimal(mktCapStr);
                                    mktCap /= 1000000000;
                                }
                            }

                            /* P/E */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"pe_ratio\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string peStr = sourceCode.Substring(0, endIndex);
                            if (peStr.Contains("&nbsp;-")) peStr = "0";
                            Decimal pe = Convert.ToDecimal(peStr);

                            /* Div, yield */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"latest_dividend-dividend_yield\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            range = sourceCode.Substring(0, endIndex);
                            string divStr, yieldStr = "";
                            midIndex = range.IndexOf("/");
                            if (midIndex != -1)
                            {
                                divStr = range.Substring(0, midIndex);
                                if (divStr.Contains("-")) divStr = "0";
                                yieldStr = range.Substring(midIndex + 1);
                                if (yieldStr.Contains("-")) yieldStr = "0";
                            }
                            else
                            {
                                divStr = "0";
                                yieldStr = "0";
                            }
                            Decimal div = Convert.ToDecimal(divStr);
                            Decimal yield = Convert.ToDecimal(yieldStr);

                            /* EPS */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"eps\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string epsStr = sourceCode.Substring(0, endIndex);
                            if (epsStr.Contains("&nbsp;-")) epsStr = "0";
                            Decimal eps = Convert.ToDecimal(epsStr);

                            /* Shares */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"shares\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string sharesStr = sourceCode.Substring(0, endIndex);
                            Decimal shares = 0;
                            if (sharesStr.ToLower().Contains("b"))
                            {
                                sharesStr = sharesStr.ToLower().Replace("b", "");
                                shares = Convert.ToDecimal(sharesStr);
                            }
                            else if (sharesStr.ToLower().Contains("m"))
                            {
                                sharesStr = sharesStr.ToLower().Replace("m", "");
                                shares = Convert.ToDecimal(sharesStr);
                                shares /= 1000;
                            }
                            else
                            {
                                if (sharesStr.Contains("&nbsp;-")) sharesStr = "0";
                                else
                                {
                                    shares = Convert.ToDecimal(sharesStr);
                                    shares /= 1000000000;
                                }
                            }

                            /* Beta */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"beta\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string betaStr = sourceCode.Substring(0, endIndex).Trim();
                            betaStr = betaStr.Replace("&nbsp;", "");

                            /* S&P/ASP 200  Inst. own */
                            startIndex = sourceCode.IndexOf("data-snapfield=\"inst_own\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf("class=\"val\"");
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            startIndex = sourceCode.IndexOf(">") + 1;
                            sourceCode = sourceCode.Substring(startIndex, sourceCode.Length - startIndex);
                            endIndex = sourceCode.IndexOf("</");
                            string instOwnStr = sourceCode.Substring(0, endIndex);
                            instOwnStr = instOwnStr.Replace("%", "");
                            if (instOwnStr.Contains("&nbsp;-")) instOwnStr = "0";
                            Int64 instOwn = Convert.ToInt64(instOwnStr);

                            if (!db.GoogleFinance_Data.Any(f => f.Date == createdDate))
                            {
                                db.GoogleFinance_Data.Add(new GoogleFinance_Data
                                {
                                    Name = name,
                                    Price = price,
                                    Date = createdDate,
                                    Group = groupWord,
                                    Avg_M_ = avg,
                                    Beta = betaStr,
                                    Div = div,
                                    EPS = eps,
                                    Open = open,
                                    P_E = pe,
                                    S_P_ASX_200 = instOwn,
                                    Yield = yield,
                                    Vol_M_ = vol,
                                    Shares_B_ = shares,
                                    Mkt_Cap_B_ = mktCap,
                                    Range_From = rangeFrom,
                                    Range_To = rangeTo,
                                    C52_Weeks_From = fiftytwoWeekFrom,
                                    C52_Weeks_To = fiftytwoWeekTo
                                });
                                db.SaveChanges();
                                newData++;
                            }
                            else
                            {
                                duplicates++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorBox.Items.Add(groupWord + " failed because of an error: " + ex);
                        }
                        #endregion
                }
                catch (Exception)
                {
                    errorBox.Items.Add("Invalid URL!");
                    MessageBox.Show("Invalid URL!");
                    textBox1.Text = "";
                }
            #endregion
                }
                else
                {
                    PrintCompleted();
                    break;
                }
            }
            errorBox.Items.Add("\n[" + DateTime.Now + "] Task Ended.\n" + newData + " saved and " + duplicates + " duplicates Found.");
        }

        private static void PrintCompleted() { MessageBox.Show("\n[" + DateTime.Now + "] Task Ended."); }
    }
}
