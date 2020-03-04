using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace NSFWDownloader
{
    [Serializable]
    public class Rule34Response
    {
        public string directory;
        public string hash;
        public string image;
        public string owner;
        public string rating;
        public string tags;
        public int id;
        public int? parent_id;
        public int score;
    }

    [Serializable]
    public class DanbooruResponse
    {
        public string md5;
        public string file_url;
        public string author;
        public string rating;
        public string tags;
        public int id;
        public int? parent_id;
        public int score;
        public string tag_string;
    }

    public enum NSFWOrder
    {
        ASCENDING_LIKE,
        DESCENDING_LIKE,
        NO_SORT,
        RANDOM
    }

    public enum BooruSpectrum
    {
        /// <summary>
        /// index.php needs to be added to the request for these
        /// </summary>
        RULE34IAN,
        /// <summary>
        /// Contains tags in 'tags_string'
        /// </summary>
        DANBOORUNIAN,
        /// <summary>
        /// Contains tags in 'tags'
        /// </summary>
        YANDERIAN
    }

    [Serializable]
    public class NSFWBooruWebsite
    {
        public string baseURL = "";
        public BooruSpectrum spectrum = BooruSpectrum.RULE34IAN;
        public int maxIndexing = 100;
        public string imgBaseURL = "";
    }

    [Serializable]
    public class NSFWSession
    {
        public string targetTag = "";
        public List<NSFWTemplate> categories = new List<NSFWTemplate>();
        public NSFWTemplate generic = new NSFWTemplate();
        public string trashTag = "";
        public NSFWBooruWebsite booruWebsite = null;
        public string booruWebsiteID = "rule34";
        public ulong ownerID = 0;
        public int viewedSetup = 0;
        public bool genericEnabled = true;
        [NonSerialized]
        public Thread associatedThread = null;
        [NonSerialized]
        public RestClient associatedRest = null;
        [NonSerialized]
        public RestClient imageRest = null;
        public List<NSFWImage> allPics = new List<NSFWImage>();
        public List<NSFWImage> formattedPics = new List<NSFWImage>();
        public Dictionary<NSFWTemplate, List<NSFWImage>> categorizedPics = new Dictionary<NSFWTemplate, List<NSFWImage>>();
        public List<NSFWImage> genericPics = new List<NSFWImage>();
        public List<NSFWImage> trashPics = new List<NSFWImage>();
    }

    [Serializable]
    public class NSFWUserInfo
    {
        /// <summary>
        /// how many images the program tries to request
        /// </summary>
        public int sessionRequestLimit = 1000;
        public NSFWTemplate genericTemplate = new NSFWTemplate();
        public List<NSFWTemplate> templates = new List<NSFWTemplate>();
        public int maxSaveDays = 90;
        public Dictionary<DateTime, NSFWSession> sessionHistory = new Dictionary<DateTime, NSFWSession>();
    }

    [Serializable]
    public class NSFWTemplate
    {
        public NSFWOrder order = NSFWOrder.DESCENDING_LIKE;
        public Dictionary<string, float> lovedTags = new Dictionary<string, float>();
        public Dictionary<string, float> likedTags = new Dictionary<string, float>();
        public Dictionary<string, float> dislikedTags = new Dictionary<string, float>();
        public Dictionary<string, float> hatedTags = new Dictionary<string, float>();
        public string prefix = "";
        /// <summary>
        /// minimal needed score to have images appear in this category
        /// </summary>
        public double minScore = 0.0;
        /// <summary>
        /// Max amount of images for this category.
        /// </summary>
        public int maxImg = 100;
        public string name = "";
    }

    /// <summary>
    /// Every NSFW image from any website needs to be compressed into this class
    /// </summary>
    public class NSFWImage
    {
        public string author;
        public string rating;
        public int score;
        public List<string> tags;
        public string imageURL;
        /// <summary>
        /// calculated for the session(using generic), not taken from responses
        /// </summary>
        public double likenessLevel = 0.0;
        /// <summary>
        /// Each image is given a file name. They cannot be identical.
        /// </summary>
        public string fileName = "";
        /// <summary>
        /// For category management. If this is empty, then the picture cannot belong to any categories.
        /// </summary>
        public Dictionary<NSFWTemplate, double> ratingsBoard = new Dictionary<NSFWTemplate, double>();
    }

    class Program
    {
        public static Dictionary<string, NSFWBooruWebsite> booruWebsites = new Dictionary<string, NSFWBooruWebsite>()
        {
            {"rule34", new NSFWBooruWebsite{ baseURL = "https://rule34.xxx/", spectrum = BooruSpectrum.RULE34IAN, maxIndexing = 100, imgBaseURL = $"https://img.rule34.xxx"} },
            {"gelbooru", new NSFWBooruWebsite{ baseURL = "https://gelbooru.com/", spectrum = BooruSpectrum.RULE34IAN, maxIndexing = 250, imgBaseURL = $"https://img2.gelbooru.com"} },
            {"yandere", new NSFWBooruWebsite{ baseURL = "https://yande.re/post.json", spectrum = BooruSpectrum.YANDERIAN, maxIndexing = 100, imgBaseURL = "https://files.yande.re/image"} },
            {"danbooru", new NSFWBooruWebsite{ baseURL = "https://danbooru.donmai.us/posts.json", spectrum = BooruSpectrum.DANBOORUNIAN, maxIndexing = 200, imgBaseURL = "https://danbooru.donmai.us/data"} }
        };

        public static NSFWSession currentSession = new NSFWSession();
        public static NSFWUserInfo userInfo = new NSFWUserInfo();

        public static string VERSION = "b3.0";

        static void Main()
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "user.json")) { File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "user.json", "{}"); }
            try
            {
                userInfo = JsonConvert.DeserializeObject<NSFWUserInfo>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "user.json"));
                currentSession.generic = new NSFWTemplate
                {
                    hatedTags = new Dictionary<string, float>(userInfo.genericTemplate.hatedTags),
                    dislikedTags = new Dictionary<string, float>(userInfo.genericTemplate.dislikedTags),
                    likedTags = new Dictionary<string, float>(userInfo.genericTemplate.likedTags),
                    lovedTags = new Dictionary<string, float>(userInfo.genericTemplate.lovedTags),
                    order = userInfo.genericTemplate.order,
                    prefix = userInfo.genericTemplate.prefix
                };
                currentSession.booruWebsite = booruWebsites[currentSession.booruWebsiteID];
                bool stay = true;
                LogLine($"NSFWDOWNLOADER VERSION {VERSION} BY HELLESSION");
                while (stay)
                {
                    LogLine("==================================================");
                    LogLine("MAIN MENU OF SESSION SETUP\n" +
                        $"You currently have {currentSession.categories.Count} categories enlisted for the session.\n" +
                        $"Your current trash tag is(if you see nothing, then it's empty): {currentSession.trashTag}\n" +
                        $"Your current TARGET tag is(if you see nothing, then it's empty): {currentSession.targetTag}\n" +
                        $"{(currentSession.genericEnabled ? "Generic tag collection is enabled, which means if none of your images go to a category, they will still end up in the same folder." : "Generic tag collection is DISABLED. This means that if none of your images go to any category, they will end up in a subfolder of the folder you selected, as 'trash'.")}\n" +
                        $"Image limit: {userInfo.sessionRequestLimit}\n" +
                        $"Website for session: {currentSession.booruWebsiteID}");
                    LogLine("Choose your action:\n" +
                        "MANAGE_GENERIC - change the generic set of tags template.\n" +
                        "MANAGE_CATEGORIES - manage the categories\n" +
                        "GENERIC_TOGGLE - toggle the presence of the generic images.\n" +
                        "TRASH_TAG <tags...> - set the trash tags to whatever you want it to be.\n" +
                        "SET_TAG <tags...> - set the main tag of what you want to download.\n" +
                        "IMAGE_LIMIT <limit> - set the image limit, the maximum amount of images that can be downloaded.\n" +
                        "WEBSITE <website> - set the website where to download the images from.(rule34/gelbooru/danbooru/yandere)\n" +
                        "START - start the download(you'll be asked where to save)!!!\n" +
                        "SAVE - save your user info to file before starting download\n" +
                        "EXIT - exit the program.");
                    List<string> userInput = ParseInput(Console.ReadLine());
                    if (userInput.Count > 0)
                    {
                        if (userInput[0] == "manage_generic")
                        {
                            ManageTags(currentSession.generic, "Generic");
                        }
                        if (userInput[0] == "manage_categories")
                        {
                            ManageCategories(currentSession);
                        }
                        if (userInput[0] == "generic_toggle")
                        {
                            currentSession.genericEnabled = !currentSession.genericEnabled;
                            if (currentSession.genericEnabled)
                            {
                                LogLine($"The generic tag collection can now hold photos and has been enabled!");
                            }
                            else
                            {
                                LogLine($"The generic tag collection can no longer hold any photos and has been disabled!");
                            }
                        }
                        if (userInput[0] == "trash_tag")
                        {
                            List<string> u = userInput.ToList();
                            u.RemoveAt(0);
                            string k = string.Join(' ', u);
                            currentSession.trashTag = k;
                            if (string.IsNullOrEmpty(k)) { k = "(Nothing!)"; }
                            LogLine($"The trash tag(s) has(have) been set to: {k}");
                        }
                        if (userInput[0] == "set_tag")
                        {
                            List<string> u = userInput.ToList();
                            u.RemoveAt(0);
                            string k = string.Join(' ', u);
                            currentSession.targetTag = k;
                            if (string.IsNullOrEmpty(k)) { k = "(Nothing!)"; }
                            LogLine($"The target tag(s) has(have) been set to: {k}");
                        }
                        if (userInput[0] == "image_limit" && userInput.Count > 1)
                        {
                            if(int.TryParse(userInput[1], out int count))
                            {
                                if(count < 1)
                                {
                                    LogLine($"Enter something above 0 please.");
                                }
                                else
                                {
                                    userInfo.sessionRequestLimit = count;
                                    LogLine($"The total image amount to request has been set to: {count}");
                                }
                            }
                            else
                            {
                                LogLine($"That's not a number!");
                            }
                        }
                        if (userInput[0] == "website" && userInput.Count > 1)
                        {
                            if (booruWebsites.ContainsKey(userInput[1]))
                            {
                                currentSession.booruWebsiteID = userInput[1];
                                currentSession.booruWebsite = booruWebsites[currentSession.booruWebsiteID];
                                LogLine($"The website has been set to {currentSession.booruWebsiteID}, image index limit: {currentSession.booruWebsite.maxIndexing}" +
                                    $", base URL: {currentSession.booruWebsite.baseURL}, base image URL: {currentSession.booruWebsite.imgBaseURL}");
                            }
                            else
                            {
                                LogLine($"That's not a website!");
                            }
                        }
                        if (userInput[0] == "start")
                        {
                            stay = false;
                        }
                        if (userInput[0] == "exit")
                        {
                            return;
                        }
                        if (userInput[0] == "save")
                        {
                            SaveFile();
                        }
                    }
                }
                SaveFile();
                Log($"Are you ready?\nEnter the destination folder where to download the stuff(empty folder or one that doesn't exist yet recommended): ");
                string dest = Console.ReadLine();
                if(string.IsNullOrEmpty(dest))
                {
                    LogLine("Come onnn, you're not supposed to leave the destination folder path empty! This program will shut down, relaunch to try again.");
                    Console.ReadKey();
                    return;
                }
                if (!Directory.Exists(dest))
                {
                    Directory.CreateDirectory(dest);
                }
                userInfo.sessionHistory.Add(DateTime.Now, currentSession);
                SaveFile();
                //This is where the download starts
                LogLine($"Step 1: Looking up {userInfo.sessionRequestLimit} pictures for the tag {currentSession.targetTag}...");
                currentSession.allPics.Clear();
                currentSession.formattedPics.Clear();
                currentSession.categorizedPics.Clear();
                currentSession.associatedRest = new RestClient(currentSession.booruWebsite.baseURL);
                if (currentSession.booruWebsite.spectrum == BooruSpectrum.RULE34IAN)
                {
                    int failedAttempts = 0;
                    bool noMore = false;
                    int page = 0;
                    while (failedAttempts < 5 && !noMore && currentSession.allPics.Count < userInfo.sessionRequestLimit)
                    {
                        IRestRequest rq =
                        new RestRequest($"index.php?page=dapi&s=post&q=index&limit={currentSession.booruWebsite.maxIndexing}&json=1{(!string.IsNullOrEmpty(currentSession.targetTag) ? $"&tags={currentSession.targetTag}" : "")}&pid={page}", Method.GET);
                        LogLine($"HTTP REQUEST: Looking for rule34 images under tag {currentSession.targetTag} page {page}...");
                        IRestResponse rs = await currentSession.associatedRest.ExecuteTaskAsync(rq);
                        if (rs.IsSuccessful)
                        {
                            if (string.IsNullOrEmpty(rs.Content))
                            {
                                noMore = true;
                                LogLine($"No images returned!");
                            }
                            else
                            {
                                List<Rule34Response> response = JsonConvert.DeserializeObject<List<Rule34Response>>(rs.Content);
                                if (response.Count == 0)
                                {
                                    noMore = true;
                                }
                                else
                                {
                                    page++;
                                    foreach (Rule34Response i in response)
                                    {
                                        if (currentSession.allPics.Count < userInfo.sessionRequestLimit)
                                        {
                                            NSFWImage img = new NSFWImage
                                            {
                                                tags = i.tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
                                                author = i.owner,
                                                score = i.score,
                                                imageURL = $"//images/{i.directory}/{i.image}",
                                                rating = i.rating
                                            };
                                            currentSession.allPics.Add(img);
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                            failedAttempts++;
                    }
                    if (noMore)
                        LogLine($"Found no more images. Allpics is {currentSession.allPics.Count} pics big.");
                    if (failedAttempts >= 5)
                        LogLine($"Failed 5 times.");
                }
                else if (currentSession.booruWebsite.spectrum == BooruSpectrum.DANBOORUNIAN)
                {
                    int failedAttempts = 0;
                    bool noMore = false;
                    int page = 1;
                    while (failedAttempts < 5 && !noMore && currentSession.allPics.Count < userInfo.sessionRequestLimit)
                    {
                        IRestRequest rq =
                        new RestRequest($"?limit={currentSession.booruWebsite.maxIndexing}{(!string.IsNullOrEmpty(currentSession.targetTag) ? $"&tags={currentSession.targetTag}" : "")}&page={page}", Method.GET);
                        LogLine($"HTTP REQUEST: Looking for danboorunian images under tag {currentSession.targetTag} page {page}...");
                        IRestResponse rs = await currentSession.associatedRest.ExecuteTaskAsync(rq);
                        if (rs.IsSuccessful)
                        {
                            if (string.IsNullOrEmpty(rs.Content))
                            {
                                noMore = true;
                                LogLine($"No images returned!");
                            }
                            else
                            {
                                List<DanbooruResponse> response = JsonConvert.DeserializeObject<List<DanbooruResponse>>(rs.Content);
                                if (response.Count == 0)
                                {
                                    noMore = true;
                                }
                                else
                                {
                                    page++;
                                    foreach (DanbooruResponse i in response)
                                    {
                                        if (currentSession.allPics.Count < userInfo.sessionRequestLimit)
                                        {
                                            NSFWImage img = new NSFWImage
                                            {
                                                tags = i.tag_string.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
                                                author = i.author,
                                                score = i.score,
                                                imageURL = i.file_url.Replace(currentSession.booruWebsite.imgBaseURL,""),
                                                rating = i.rating
                                            };
                                            currentSession.allPics.Add(img);
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                            failedAttempts++;
                    }
                    if (noMore)
                        LogLine($"Found no more images. Allpics is {currentSession.allPics.Count} pics big.");
                    if (failedAttempts >= 5)
                        LogLine($"Failed 5 times.");
                }
                else if (currentSession.booruWebsite.spectrum == BooruSpectrum.YANDERIAN)
                {
                    int failedAttempts = 0;
                    bool noMore = false;
                    int page = 1;
                    while (failedAttempts < 5 && !noMore && currentSession.allPics.Count < userInfo.sessionRequestLimit)
                    {
                        IRestRequest rq =
                        new RestRequest($"?limit={currentSession.booruWebsite.maxIndexing}{(!string.IsNullOrEmpty(currentSession.targetTag) ? $"&tags={currentSession.targetTag}" : "")}&page={page}", Method.GET);
                        LogLine($"HTTP REQUEST: Looking for yanderian images under tag {currentSession.targetTag} page {page}...");
                        IRestResponse rs = await currentSession.associatedRest.ExecuteTaskAsync(rq);
                        if (rs.IsSuccessful)
                        {
                            if (string.IsNullOrEmpty(rs.Content))
                            {
                                noMore = true;
                                LogLine($"No images returned!");
                            }
                            else
                            {
                                List<DanbooruResponse> response = JsonConvert.DeserializeObject<List<DanbooruResponse>>(rs.Content);
                                if (response.Count == 0)
                                {
                                    noMore = true;
                                }
                                else
                                {
                                    page++;
                                    foreach (DanbooruResponse i in response)
                                    {
                                        if (currentSession.allPics.Count < userInfo.sessionRequestLimit)
                                        {
                                            NSFWImage img = new NSFWImage
                                            {
                                                tags = i.tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
                                                author = i.author,
                                                score = i.score,
                                                imageURL = i.file_url.Replace(currentSession.booruWebsite.imgBaseURL, ""),
                                                rating = i.rating
                                            };
                                            currentSession.allPics.Add(img);
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                            failedAttempts++;
                    }
                    if (noMore)
                        LogLine($"Found no more images. Allpics is {currentSession.allPics.Count} pics big.");
                    if (failedAttempts >= 5)
                        LogLine($"Failed 5 times.");
                }
                //if there are categories, generic hated/loved are not yet applied.
                //for now, loved tags will simply give +1000 and hated tags will simply give -1000
                //generic sort 1
                LogLine($"Step 2: Collected {currentSession.allPics.Count}. APPLYING GENERIC DESCENDING SORT...");
                foreach (NSFWImage i in currentSession.allPics)
                {
                    foreach(KeyValuePair<string, float> k in currentSession.generic.lovedTags)
                    {
                        if (i.tags.Contains(k.Key))
                            i.likenessLevel += 1000;
                    }
                    foreach (KeyValuePair<string, float> k in currentSession.generic.likedTags)
                    {
                        if (i.tags.Contains(k.Key))
                            i.likenessLevel += k.Value;
                    }
                    foreach (KeyValuePair<string, float> k in currentSession.generic.dislikedTags)
                    {
                        if (i.tags.Contains(k.Key))
                            i.likenessLevel -= k.Value;
                    }
                    foreach (KeyValuePair<string, float> k in currentSession.generic.hatedTags)
                    {
                        if (i.tags.Contains(k.Key))
                            i.likenessLevel -= 1000;
                    }
                }
                //sort by descending for upcoming category madness!!!
                currentSession.allPics = (from g in currentSession.allPics orderby g.likenessLevel descending select g).ToList();
                // FROM THIS POINT ON, THAT PERSONAL RATING STUFF DOESN'T EVEN MATTER
                List<string> trash = new List<string>();
                if(currentSession.trashTag != null)
                {
                    trash = currentSession.trashTag.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                if(currentSession.categories.Count > 0)
                {
                    //ok so...
                    LogLine($"Step 3: CATEGORIZED ASSIGNATION...");
                    foreach(NSFWTemplate k in currentSession.categories)
                    {
                        currentSession.categorizedPics.Add(k, new List<NSFWImage>());
                    }
                    foreach(NSFWImage i in currentSession.allPics)
                    {
                        CategorizePicture(i, trash);
                        if(!currentSession.trashPics.Contains(i))
                        foreach(KeyValuePair<NSFWTemplate, double> k in i.ratingsBoard)
                        {
                            currentSession.categorizedPics[k.Key].Add(i);
                        }
                    }
                    //now let's sort each category
                    LogLine($"Step 3a: CATEGORIZED LIKE SORTING...");
                    Dictionary<NSFWTemplate, List<NSFWImage>> l = new Dictionary<NSFWTemplate, List<NSFWImage>>();
                    foreach (KeyValuePair<NSFWTemplate, List<NSFWImage>> i in currentSession.categorizedPics)
                    {
                        List<NSFWImage> d;
                        switch(i.Key.order)
                        {
                            case NSFWOrder.ASCENDING_LIKE:
                                d = (from g in i.Value orderby g.ratingsBoard[i.Key] select g).ToList();
                                l.Add(i.Key, d);
                                //If the size is larger than the largest pic number, shorten it(AT THE START)
                                if (d.Count > i.Key.maxImg)
                                {
                                    d.RemoveRange(0, d.Count - i.Key.maxImg);
                                }
                                break;
                            case NSFWOrder.DESCENDING_LIKE:
                                d = (from g in i.Value orderby g.ratingsBoard[i.Key] select g).ToList();
                                l.Add(i.Key, d);
                                //If the size is larger than the largest pic number, shorten it
                                if (d.Count > i.Key.maxImg)
                                {
                                    d.RemoveRange(i.Key.maxImg, d.Count - i.Key.maxImg);
                                }
                                break;
                            case NSFWOrder.NO_SORT:
                                d = i.Value;
                                l.Add(i.Key, d);
                                //If the size is larger than the largest pic number, shorten it
                                if (d.Count > i.Key.maxImg)
                                {
                                    d.RemoveRange(i.Key.maxImg, d.Count - i.Key.maxImg);
                                }
                                break;
                            case NSFWOrder.RANDOM:
                                d = RandomizeList(i.Value);
                                l.Add(i.Key, d);
                                //If the size is larger than the largest pic number, shorten it
                                if (d.Count > i.Key.maxImg)
                                {
                                    d.RemoveRange(i.Key.maxImg, d.Count - i.Key.maxImg);
                                }
                                break;
                            default:
                                d = new List<NSFWImage>();
                                l.Add(i.Key, d);
                                break;
                        }
                        //Remove all duplicates in other categories
                        foreach (KeyValuePair<NSFWTemplate, List<NSFWImage>> i2 in currentSession.categorizedPics)
                        {
                            if(i2.Key != i.Key)
                            {
                                foreach(NSFWImage p in d)
                                {
                                    if(i2.Value.Contains(p))
                                    {
                                        i2.Value.Remove(p);
                                    }
                                }
                            }
                        }
                    }
                    currentSession.categorizedPics = l;
                }
                LogLine($"Step 3b: GENERIC LIKE SORTING(ROUND 2)...");
                currentSession.genericPics = GetVirginImages();
                switch (currentSession.generic.order)
                {
                    case NSFWOrder.ASCENDING_LIKE:
                        currentSession.genericPics = (from g in currentSession.genericPics orderby g.likenessLevel select g).ToList();
                        //If the size is larger than the largest pic number, shorten it(AT THE START)
                        if (currentSession.genericPics.Count > currentSession.generic.maxImg)
                        {
                            currentSession.genericPics.RemoveRange(0, currentSession.genericPics.Count - currentSession.generic.maxImg);
                        }
                        break;
                    case NSFWOrder.DESCENDING_LIKE:
                        currentSession.genericPics = (from g in currentSession.genericPics orderby g.likenessLevel select g).ToList();
                        //If the size is larger than the largest pic number, shorten it
                        if (currentSession.genericPics.Count > currentSession.generic.maxImg)
                        {
                            currentSession.genericPics.RemoveRange(currentSession.generic.maxImg, currentSession.genericPics.Count - currentSession.generic.maxImg);
                        }
                        break;
                    case NSFWOrder.NO_SORT:
                        //If the size is larger than the largest pic number, shorten it
                        if (currentSession.genericPics.Count > currentSession.generic.maxImg)
                        {
                            currentSession.genericPics.RemoveRange(currentSession.generic.maxImg, currentSession.genericPics.Count - currentSession.generic.maxImg);
                        }
                        break;
                    case NSFWOrder.RANDOM:
                        currentSession.genericPics = RandomizeList(currentSession.genericPics);
                        //If the size is larger than the largest pic number, shorten it
                        if (currentSession.genericPics.Count > currentSession.generic.maxImg)
                        {
                            currentSession.genericPics.RemoveRange(currentSession.generic.maxImg, currentSession.genericPics.Count - currentSession.generic.maxImg);
                        }
                        break;
                    default:
                        currentSession.genericPics = new List<NSFWImage>();
                        break;
                }
                LogLine($"Step 4: IMPLEMENTING UNIQUE NAMING...");
                double lowestNumber = 0.0;
                foreach (KeyValuePair<NSFWTemplate, List<NSFWImage>> i in currentSession.categorizedPics)
                {
                    double highestNumber = GetHighestNumber(from g in i.Value select g.ratingsBoard[i.Key]);
                    lowestNumber = GetLowestNumber(from g in i.Value select g.ratingsBoard[i.Key]) * -1;
                    foreach (NSFWImage i2 in i.Value)
                    {
                        double rating = i2.ratingsBoard[i.Key] + lowestNumber;
                        if(i.Key.order == NSFWOrder.DESCENDING_LIKE)
                        {
                            rating = highestNumber - i2.ratingsBoard[i.Key];
                        }
                        string selectedName = $"{i.Key.prefix}{rating}{Path.GetExtension(i2.imageURL)}";
                        int stalemates = -1;
                        while(NameAlreadyExists(selectedName))
                        {
                            stalemates++;
                            selectedName = $"{i.Key.prefix}{rating}_{stalemates}{Path.GetExtension(i2.imageURL)}";
                        }
                        i2.fileName = selectedName;
                    }
                }
                lowestNumber = GetLowestNumber(from g in currentSession.genericPics select g.likenessLevel) * -1;
                foreach (NSFWImage i in currentSession.genericPics)
                {
                    string selectedName = $"generic_{i.likenessLevel + lowestNumber}{Path.GetExtension(i.imageURL)}";
                    int stalemates = -1;
                    while (NameAlreadyExists(selectedName))
                    {
                        stalemates++;
                        selectedName = $"generic_{i.likenessLevel + lowestNumber}_{stalemates}{Path.GetExtension(i.imageURL)}";
                    }
                    i.fileName = selectedName;
                }
                lowestNumber = GetLowestNumber(from g in currentSession.trashPics select g.likenessLevel) * -1;
                foreach (NSFWImage i in currentSession.trashPics)
                {
                    string selectedName = $"trash_{i.likenessLevel + lowestNumber}{Path.GetExtension(i.imageURL)}";
                    int stalemates = -1;
                    while (NameAlreadyExists(selectedName))
                    {
                        stalemates++;
                        selectedName = $"trash_{i.likenessLevel + lowestNumber}_{stalemates}{Path.GetExtension(i.imageURL)}";
                    }
                    i.fileName = selectedName;
                }
                LogLine("==================================================");
                LogLine($"HERE ARE ALL THE IMAGES COLLECTED");
                LogLine("==================================================");
                LogLine($"TOTAL: {currentSession.allPics.Count}");
                LogLine($"TRASH: {currentSession.trashPics.Count}");
                LogLine($"GENERIC: {currentSession.genericPics.Count}");
                for(int i=0;i<currentSession.categories.Count;i++)
                {
                    NSFWTemplate t = currentSession.categories[i];
                    LogLine($"CATEGORY {t.name}(PF {t.prefix}): {currentSession.categorizedPics[t].Count}");
                }
                LogLine("==================================================");
                LogLine($"Step 5: IT'S TIME TO DOWNLOAD!");
                LogLine("==================================================");
                currentSession.imageRest = new RestClient(currentSession.booruWebsite.imgBaseURL);
                int total = currentSession.trashPics.Count + currentSession.genericPics.Count;
                foreach(NSFWTemplate i in currentSession.categories) { total += currentSession.categorizedPics[i].Count; }
                int progress = 0;
                if (currentSession.trashPics.Count > 0)
                {
                    LogLine($"Time to download the trash first. Get to the good stuff later.");
                    if (!Directory.Exists(dest + "\\trash"))
                        Directory.CreateDirectory(dest + "\\trash");
                    foreach(NSFWImage i in currentSession.trashPics)
                    {
                        IRestRequest trashDn = new RestRequest(i.imageURL);
                        LogLine($"{Math.Round((double)progress/total*1000)/10}% - DL - TRASH - {i.likenessLevel} - {i.imageURL}");
                        IRestResponse trashDnD = await currentSession.imageRest.ExecuteTaskAsync(trashDn);
                        progress++;
                        if(trashDnD.IsSuccessful)
                        {
                            LogLine($"{Math.Round((double)progress / total * 1000) / 10}% - SUCCESSFUL DL - TRASH - {i.likenessLevel} - {i.imageURL}");
                            File.WriteAllBytes(dest + "\\trash\\" + i.fileName, trashDnD.RawBytes);
                        }
                        else
                        {
                            LogLine($"{Math.Round((double)progress / total * 1000) / 10}% - FAILURE DL - TRASH - {i.likenessLevel} - {i.imageURL} - STATUS: {trashDnD.StatusDescription} - MESSAGE: {trashDnD.Content}");
                        }
                    }
                }
                if (currentSession.genericPics.Count > 0)
                {
                    LogLine($"Downloading generic images.");
                    if (!currentSession.genericEnabled && !Directory.Exists(dest + "\\generic"))
                        Directory.CreateDirectory(dest + "\\generic");
                    foreach (NSFWImage i in currentSession.genericPics)
                    {
                        IRestRequest trashDn = new RestRequest(i.imageURL);
                        LogLine($"{Math.Round((double)progress / total * 1000) / 10}% - DL - GENERIC - {i.likenessLevel} - {i.imageURL}");
                        IRestResponse trashDnD = await currentSession.imageRest.ExecuteTaskAsync(trashDn);
                        progress++;
                        if (trashDnD.IsSuccessful)
                        {
                            LogLine($"{Math.Round((double)progress / total * 1000) / 10}% - SUCCESSFUL DL - GENERIC - {i.likenessLevel} - {i.imageURL}");
                            if(currentSession.genericEnabled)
                            File.WriteAllBytes(dest + "\\" + i.fileName, trashDnD.RawBytes);
                            else
                                File.WriteAllBytes(dest + "\\generic\\" + i.fileName, trashDnD.RawBytes);
                        }
                        else
                        {
                            LogLine($"{Math.Round((double)progress / total * 1000) / 10}% - FAILURE DL - GENERIC - {i.likenessLevel} - {i.imageURL} - STATUS: {trashDnD.StatusDescription} - MESSAGE: {trashDnD.Content}");
                        }
                    }
                }
                foreach(NSFWTemplate i in currentSession.categories)
                {
                    LogLine($"Downloading category {currentSession.categories.IndexOf(i)+1}(pf {i.prefix}) images.");
                    foreach (NSFWImage k in currentSession.categorizedPics[i])
                    {
                        IRestRequest trashDn = new RestRequest(k.imageURL);
                        LogLine($"{Math.Round((double)progress / total * 1000) / 10}% - DL - CATEGORY {i.name} - {k.likenessLevel} - {k.imageURL}");
                        IRestResponse trashDnD = await currentSession.imageRest.ExecuteTaskAsync(trashDn);
                        progress++;
                        if (trashDnD.IsSuccessful)
                        {
                            LogLine($"{Math.Round((double)progress / total * 1000) / 10}% - SUCCESSFUL DL - CATEGORY {i.name} - {k.likenessLevel} - {k.imageURL}");
                            File.WriteAllBytes(dest + "\\" + k.fileName, trashDnD.RawBytes);
                        }
                        else
                        {
                            LogLine($"{Math.Round((double)progress / total * 1000) / 10}% - FAILURE DL - CATEGORY {i.name} - {k.likenessLevel} - {k.imageURL} - STATUS: {trashDnD.StatusDescription} - MESSAGE: {trashDnD.Content}");
                        }
                    }
                }
                LogLine($"PROCESS COMPLETE! Press any key to close the app and enjoy. :)");
                Console.ReadKey();
            }
            catch(Exception e)
            {
                LogLine($"EXCEPTION FACED: {e.Message}\n{e.StackTrace}");
                Console.ReadKey();
            }
        }

        private static double GetLowestNumber(IEnumerable<double> numbers)
        {
            double lowest = 10000.0;
            foreach(double i in numbers)
            {
                if(i < lowest)
                {
                    lowest = i;
                }
            }
            return lowest;
        }

        private static double GetHighestNumber(IEnumerable<double> numbers)
        {
            double highest = -100000;
            foreach (double i in numbers)
            {
                if (i > highest)
                {
                    highest = i;
                }
            }
            return highest;
        }

        private static bool NameAlreadyExists(string name)
        {
            foreach(NSFWImage i in currentSession.allPics)
            {
                if (i.fileName == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<NSFWImage> GetVirginImages()
        {
            List<NSFWImage> result = currentSession.allPics.ToList();
            foreach(KeyValuePair<NSFWTemplate, List<NSFWImage>> i in currentSession.categorizedPics)
            {
                foreach(NSFWImage i2 in i.Value)
                {
                    if(result.Contains(i2))
                    {
                        result.Remove(i2);
                    }
                }
            }
            foreach(NSFWImage i in currentSession.trashPics)
            {
                result.Remove(i);
            }
            for(int k=0;k<result.Count;k++)
            {
                NSFWImage i = result[k];
                foreach(KeyValuePair<string, float> p in currentSession.generic.hatedTags)
                {
                    if(i.tags.Contains(p.Key))
                    {
                        k--;
                        result.Remove(i);
                        continue;
                    }
                }
                foreach (KeyValuePair<string, float> p in currentSession.generic.lovedTags)
                {
                    if (!i.tags.Contains(p.Key))
                    {
                        k--;
                        result.Remove(i);
                        continue;
                    }
                }
            }
            return result;
        }

        public static List<NSFWImage> RandomizeList(List<NSFWImage> value)
        {
            List<NSFWImage> k = new List<NSFWImage>();
            foreach(NSFWImage i in value)
            {
                k.Insert(Random.GetRandomInt(0, k.Count - 1), i);
            }
            return k;
        }

        private static void CategorizePicture(NSFWImage i, List<string> trash)
        {
            foreach (string k in trash)
            {
                if (i.tags.Contains(k))
                {
                    currentSession.trashPics.Add(i);
                    return;
                }
            }
            foreach (NSFWTemplate t in currentSession.categories)
            {
                bool viableForCategory = true;
                double currentRating = 0.0;
                //firstly check the loved/hated tags
                foreach (KeyValuePair<string, float> k in t.lovedTags)
                {
                    if (!i.tags.Contains(k.Key))
                        viableForCategory = false;
                }
                foreach (KeyValuePair<string, float> k in t.hatedTags)
                {
                    if (i.tags.Contains(k.Key))
                        viableForCategory = false;
                }
                if (!viableForCategory) continue;
                //secondly use the liked/disliked tags to get the rating
                foreach (KeyValuePair<string, float> k in t.likedTags)
                {
                    if (i.tags.Contains(k.Key))
                        currentRating += k.Value;
                }
                foreach (KeyValuePair<string, float> k in t.dislikedTags)
                {
                    if (i.tags.Contains(k.Key))
                        currentRating -= k.Value;
                }
                //finally do a final check, compare if the template minimal score is met
                if (currentRating >= t.minScore)
                {
                    i.ratingsBoard.Add(t, currentRating);
                }
            }
        }

        static void SaveFile()
        {
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "user.json", JsonConvert.SerializeObject(userInfo));
            LogLine("Saved user info to file.");
        }

        static void ManageCategories(NSFWSession session)
        {
            bool stay = true;
            while(stay)
            {
                LogLine("==================================================");
                LogLine("CATEGORY MANAGEMENT");
                LogLine("==================================================");
                LogLine($"You currently have {session.categories.Count} categories enlisted for the session.");
                if(session.categories.Count > 0)
                {
                    string line = $"1 - {session.categories[0].name}";
                    for(int i=1;i<session.categories.Count;i++)
                    {
                        line += $"\n{i + 1} - {session.categories[i].name}";
                    }
                    LogLine(line);
                }
                LogLine($"EDIT <number> - to edit a certain category under that number.\n" +
                    $"NEW - to create a new category!\n" +
                    $"REMOVE <number> - remove a certain category from the session under that number.\n" +
                    $"Type anything else and you'll exit back into session menu.");
                List<string> userInput = ParseInput(Console.ReadLine());
                if (userInput.Count > 0)
                {
                    if (userInput[0] == "edit")
                    {
                        if(int.TryParse(userInput[1], out int index))
                        {
                            index--;
                            if(index > -1 && index < session.categories.Count)
                            {
                                ManageTags(session.categories[index], $"Category {index+1}");
                            }
                            else
                            {
                                LogLine("There is no category under that number!");
                            }
                        }
                        else
                        {
                            LogLine("In order to address a category, you need to provide its number! You've clearly not given me a number!");
                        }
                    }
                    else if (userInput[0] == "remove")
                    {
                        if (int.TryParse(userInput[1], out int index))
                        {
                            index--;
                            if (index > -1 && index < session.categories.Count)
                            {
                                session.categories.RemoveAt(index);
                                LogLine($"Session {index + 1} has been removed.");
                            }
                            else
                            {
                                LogLine("There is no category under that number!");
                            }
                        }
                        else
                        {
                            LogLine("In order to address a category, you need to provide its number! You've clearly not given me a number!");
                        }
                    }
                    else if(userInput[0] == "new")
                    {
                        session.categories.Add(new NSFWTemplate());
                        LogLine($"Category {session.categories.Count} has been created!");
                        ManageTags(session.categories[session.categories.Count - 1], $"Category {session.categories.Count}");
                    }
                    else
                    {
                        stay = false;
                    }
                }
                else
                {
                    stay = false;
                }
            }
        }

        static string TagDictToString(Dictionary<string, float> d)
        {
            string result = "";
            foreach(KeyValuePair<string, float> i in d)
            {
                result += $"{{{i.Key} {i.Value}}} ";
            }
            return result;
        }

        static void ManageTags(NSFWTemplate toManage, string manageName)
        {
            bool stay = true;
            while(stay)
            {
                LogLine("==================================================");
                LogLine("TAG COLLECTION MANAGEMENT");
                LogLine("==================================================");
                LogLine($"Tag collection {toManage.name}:\n" +
                    $"LOVED TAGS - {TagDictToString(toManage.lovedTags)}\n" +
                    $"LIKED TAGS - {TagDictToString(toManage.likedTags)}\n" +
                    $"DISLIKED TAGS - {TagDictToString(toManage.dislikedTags)}\n" +
                    $"HATED TAGS - {TagDictToString(toManage.hatedTags)}\n" +
                    $"PREFIX - {toManage.prefix}\n" +
                    $"ORDER - {OrderToString(toManage.order)}\n" +
                    $"MINIMAL SCORE - {toManage.minScore}\n" +
                    $"MAXIMAL IMAGE COUNT - {toManage.maxImg}");
                LogLine($"Change Loved/Liked/Disliked/Hated tags by using the format: <loved/liked/disliked/hated> <add/remove/replace> <tag> [float]\n" +
                    $"'add' adds the tags to the total(to add more than one, separate tags and floats with ,,,), 'replace' replaces the total with what you give(for more than one replaced tag, separate tags and floats with ,,,), 'remove' removes tags you give from the total.\n" +
                    $"For example: liked add vaginal_penetration cowgirl saliva,,,1 4 0.2\n" +
                    $"Or: disliked replace rape futanari,,,1 7.5\n" +
                    $"Change the prefix: prefix <prefix>\n" +
                    $"Change the order: order <ascending/descending/random/none>\n" +
                    $"Change minimal score: score <score>\n" +
                    $"Change max image count: count <count>\n" +
                    $"Change name: name <name>\n" +
                    $"Type 'load <1,2,3,.../generic>' to load up a template into this.\n" +
                    $"Type 'save <new/1,2,3,.../generic>' to save these preferences as a template.\n" +
                    $"Type anything else and the tag manager will exit.");
                List<string> userInput = ParseInput(Console.ReadLine());
                if(userInput.Count > 0)
                {
                    //fuck I really need to start puting switch stuff soon
                    if(userInput[0] == "loved" || userInput[0] == "lo")
                    {
                        WorkTagList(userInput, toManage.lovedTags, true);
                    }
                    else if (userInput[0] == "liked" || userInput[0] == "li")
                    {
                        WorkTagList(userInput, toManage.likedTags, false);
                    }
                    else if (userInput[0] == "disliked" || userInput[0] == "d")
                    {
                        WorkTagList(userInput, toManage.dislikedTags, false);
                    }
                    else if (userInput[0] == "hated" || userInput[0] == "h")
                    {
                        WorkTagList(userInput, toManage.hatedTags, true);
                    }
                    else if(userInput[0] == "prefix")
                    {
                        toManage.prefix = userInput[1];
                        LogLine("Set your prefix to: " + toManage.prefix);
                    }
                    else if(userInput[0] == "order")
                    {
                        switch(userInput[1])
                        {
                            case "ascending":
                                toManage.order = NSFWOrder.ASCENDING_LIKE;
                                break;
                            case "descending":
                                toManage.order = NSFWOrder.DESCENDING_LIKE;
                                break;
                            case "random":
                                toManage.order = NSFWOrder.RANDOM;
                                break;
                            case "none":
                                toManage.order = NSFWOrder.NO_SORT;
                                break;
                            default:
                                LogLine("The order can only be set to ascending, descending, random or none.");
                                break;
                        }
                    }
                    else if(userInput[0] == "count")
                    {
                        if(int.TryParse(userInput[1], out int result))
                        {
                            toManage.maxImg = result;
                            LogLine($"The maximal image count that the category can contain has been set to {result}");
                        }
                        else
                        {
                            LogLine("That is not an integer number. The image count can only be set to an integer.");
                        }
                    }
                    else if (userInput[0] == "score")
                    {
                        if (int.TryParse(userInput[1], out int result))
                        {
                            toManage.minScore = result;
                            LogLine($"The minimal score for the images to appear in the category has been set to {result}");
                        }
                        else
                        {
                            LogLine("That is not an integer number. The score can only be set to an integer.");
                        }
                    }
                    else if (userInput[0] == "name")
                    {
                        List<string> boohoo = userInput.ToList();
                        boohoo.RemoveAt(0);
                        toManage.name = string.Join(' ', boohoo);
                        LogLine($"Changed the name of the tag collection to {toManage.name}!");
                    }
                    else if(userInput[0] == "load")
                    {
                        if(userInput.Count > 1 && int.TryParse(userInput[1], out int index))
                        {
                            index--;
                            if(index > -1 && index < userInfo.templates.Count)
                            {
                                LogLine($"Loading template of index {index+1} into this collection!");
                                toManage.hatedTags = new Dictionary<string, float>(userInfo.templates[index].hatedTags);
                                toManage.dislikedTags = new Dictionary<string, float>(userInfo.templates[index].dislikedTags);
                                toManage.likedTags = new Dictionary<string, float>(userInfo.templates[index].likedTags);
                                toManage.lovedTags = new Dictionary<string, float>(userInfo.templates[index].lovedTags);
                                toManage.order = userInfo.templates[index].order;
                                toManage.prefix = userInfo.templates[index].prefix;
                            }
                            else
                            {
                                LogLine($"The template under this number is not known!");
                            }
                        }
                        else if(userInput.Count > 1 && userInput[1] == "generic")
                        {
                            LogLine("Loading the generic tag collection into this collection!");
                            toManage.hatedTags = new Dictionary<string, float>(userInfo.genericTemplate.hatedTags);
                            toManage.dislikedTags = new Dictionary<string, float>(userInfo.genericTemplate.dislikedTags);
                            toManage.likedTags = new Dictionary<string, float>(userInfo.genericTemplate.likedTags);
                            toManage.lovedTags = new Dictionary<string, float>(userInfo.genericTemplate.lovedTags);
                            toManage.order = userInfo.genericTemplate.order;
                            toManage.prefix = userInfo.genericTemplate.prefix;
                        }
                        else
                        {
                            LogLine("I don't know what tag collection you want to load into the current one. You can select 'generic' to use the generic one or to use a template, type out its index(starting from 1).");
                        }
                    }
                    else if (userInput[0] == "save")
                    {
                        if (userInput.Count > 1 && int.TryParse(userInput[1], out int index))
                        {
                            index--;
                            if (index > -1 && index < userInfo.templates.Count)
                            {
                                LogLine($"Saving this tag collection as a template, overwriting index {index + 1}!");
                                userInfo.templates[index].hatedTags = new Dictionary<string, float>(toManage.hatedTags);
                                userInfo.templates[index].dislikedTags = new Dictionary<string, float>(toManage.dislikedTags);
                                userInfo.templates[index].likedTags = new Dictionary<string, float>(toManage.likedTags);
                                userInfo.templates[index].lovedTags = new Dictionary<string, float>(toManage.lovedTags);
                                userInfo.templates[index].order = toManage.order;
                                userInfo.templates[index].prefix = toManage.prefix;
                            }
                            else
                            {
                                LogLine($"The template under this number is not known!");
                            }
                        }
                        else if (userInput.Count > 1 && userInput[1] == "generic")
                        {
                            LogLine($"Saving this tag collection as the generic tag collection! Note: This will not affect the generic tag collection currently in use for the session setup!");
                            userInfo.genericTemplate.hatedTags = new Dictionary<string, float>(toManage.hatedTags);
                            userInfo.genericTemplate.dislikedTags = new Dictionary<string, float>(toManage.dislikedTags);
                            userInfo.genericTemplate.likedTags = new Dictionary<string, float>(toManage.likedTags);
                            userInfo.genericTemplate.lovedTags = new Dictionary<string, float>(toManage.lovedTags);
                            userInfo.genericTemplate.order = toManage.order;
                            userInfo.genericTemplate.prefix = toManage.prefix;
                        }
                        else if(userInput.Count > 1 && userInput[1] == "new")
                        {
                            userInfo.templates.Add(new NSFWTemplate
                            {
                                hatedTags = new Dictionary<string, float>(toManage.hatedTags),
                                dislikedTags = new Dictionary<string, float>(toManage.dislikedTags),
                                likedTags = new Dictionary<string, float>(toManage.likedTags),
                                lovedTags = new Dictionary<string, float>(toManage.lovedTags),
                                order = toManage.order,
                                prefix = toManage.prefix
                            });
                            LogLine($"Saving this tag collection as the new collection, numbered {userInfo.templates.Count}.");
                        }
                        else
                        {
                            LogLine("I don't know what tag collection you want to save this into. You can select 'generic' to use the generic one, 'new' to create a new template or to replace a template, type out its index(starting from 1).");
                        }
                    }
                    else
                    {
                        stay = false;
                    }
                }
                else
                {
                    stay = false;
                }
            }
        }

        static void WorkTagList(List<string> userInput, Dictionary<string, float> managementEase, bool lovedHated)
        {
            if(userInput.Count < 2) { LogLine("Not enough arguments were passed.");return; }
            if (userInput[1] == "tags")
                userInput.RemoveAt(1);
            if (userInput[1] == "add")
            {
                Dictionary<string, float> newAddition = WorkOutTags(userInput, lovedHated);
                if (newAddition == null) { LogLine("Aborting operation because there was an error with parsing the tags you provided."); return; }
                foreach (KeyValuePair<string, float> i in newAddition)
                {
                    if (!managementEase.Keys.Contains(i.Key))
                    {
                        managementEase.Add(i.Key, i.Value);
                    }
                    else
                    {
                        LogLine($"Ignoring the tag addition {i.Key} because it already exists! Replace it instead.");
                    }
                }
                LogLine($"Added {newAddition.Count} {userInput[0]} tags(except the duplicates).");
            }
            else if (userInput[1] == "replace")
            {
                Dictionary<string, float> newAddition = WorkOutTags(userInput, lovedHated);
                if(newAddition == null) { LogLine("Aborting operation because there was an error with parsing the tags you provided."); return; }
                int successReplace = 0;
                foreach (KeyValuePair<string, float> i in newAddition)
                {
                    if (managementEase.Keys.Contains(i.Key))
                    {
                        managementEase[i.Key] = i.Value;
                        successReplace++;
                    }
                    else
                    {
                        LogLine($"The tag {i.Key} does not exist. Ignoring.");
                    }
                }
                LogLine($"Replaced {successReplace} out of {newAddition.Count} {userInput[0]} tags.");
            }
            else if (userInput[1] == "remove")
            {
                List<string> u = userInput.ToList();
                u.RemoveAt(0);
                u.RemoveAt(0);
                int successRemove = 0;
                foreach (string i in u)
                {
                    if (managementEase.Keys.Contains(i))
                    {
                        managementEase.Remove(i);
                        successRemove++;
                    }
                    else
                    {
                        LogLine($"The tag {i} does not exist. Ignoring.");
                    }
                }
                LogLine($"Removed {successRemove} out of {u.Count} {userInput[0]} tags.");
            }
            else
            {
                LogLine($"Unknown operation! Can only be: add, replace and remove.");
            }
        }

        static Dictionary<string, float> WorkOutTags(List<string> u, bool hatedLoved)
        {
            List<string> userInput = u.ToList();
            //first of all, condense the input into one, after removing the first and second entry...
            userInput.RemoveAt(0);
            userInput.RemoveAt(0);
            //the only real place where finale is used is if there is a triple comma
            string finale = string.Join(' ', userInput);
            if(string.IsNullOrEmpty(finale))
            {
                LogLine("Tag parsing WARNING: Given empty string.");
                return new Dictionary<string, float>();
            }
            else if(userInput.Count == 1)
            {
                if (hatedLoved)
                {
                    LogLine("Tag parsing: Going with the assumption that there's one tag here for loved/hated.");
                    return new Dictionary<string, float>
                    {
                        {userInput[0], 0 }
                    };
                }
                else
                {
                    LogLine("Tag parsing CRITICAL: Providing just the tag is not enough, you also need to configure its floating point number representing the weight of the like/dislike!");
                    return null;
                }
            }
            else if(userInput.Count == 2)
            {
                if(float.TryParse(userInput[1], out float val))
                {
                    LogLine("Tag parsing: Going with the assumption that there's only one tag with a float here.");
                    return new Dictionary<string, float>
                    {
                        {userInput[0], val }
                    };
                }
                else if(hatedLoved)
                {
                    LogLine("Tag parsing: Going with the assumption that there are two tags here for loved/hated.");
                    return new Dictionary<string, float>
                    {
                        {userInput[0], 0 },
                        {userInput[1], 0 }
                    };
                }
                else
                {
                    LogLine("Tag parsing CRITICAL: The 2nd value after the space is not a floating point number!");
                    return null;
                }
            }
            else
            {
                if(finale.Contains(",,,"))
                {
                    string[] finaleSplitUp = finale.Split(",,,");
                    List<string> keysList = finaleSplitUp[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                    List<string> valuesListRaw = finaleSplitUp[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                    //1. Check if key and value length is the same
                    if(keysList.Count == valuesListRaw.Count)
                    {
                        List<float> valuesList = new List<float>();
                        foreach(string i in valuesListRaw)
                        {
                            if(float.TryParse(i, out float value))
                            {
                                valuesList.Add(value);
                            }
                            else
                            {
                                LogLine($"Tag parsing CRITICAL: The value '{i}', item number {valuesListRaw.IndexOf(i)+1} of weights, is not a floating point number!");
                                return null;
                            }
                        }
                        Dictionary<string, float> result = new Dictionary<string, float>();
                        for(int i=0;i<keysList.Count;i++)
                        {
                            if(!result.Keys.Contains(keysList[i]))
                            {
                                result.Add(keysList[i], valuesList[i]);
                            }
                            else
                            {
                                LogLine($"Tag parsing WARNING: Skipping duplicate tag '{keysList[i]}' with the weight '{valuesList[i]}' - they have already been assigned before!");
                            }
                        }
                        return result;
                    }
                    else
                    {
                        LogLine($"Tag parsing CRITICAL: The amount of tags there are mentioned doesn't equal the amount of their corresponding weights after the ',,,'! ({keysList.Count} / {valuesListRaw.Count})");
                        return null;
                    }
                }
                else if(hatedLoved)
                {
                    LogLine($"Tag parsing: Because this is a hated/loved tag parsing, assuming all float values are 0.");
                    //if this is hated/loved their floats can be set to 0
                    Dictionary<string, float> result = new Dictionary<string, float>();
                    foreach(string i in userInput)
                    {
                        result.Add(i, 0);
                    }
                    return result;
                }
                else
                {
                    //try seeing if the tags are alligned like: tag float tag float tag float, etc.
                    bool failure = false;
                    Dictionary<string, float> result = new Dictionary<string, float>();
                    bool odd = false;
                    string tag = "";
                    foreach (string i in userInput)
                    {
                        if(!odd)
                        {
                            tag = i;
                        }
                        else
                        {
                            if(float.TryParse(i, out float tagFloat))
                            {
                                result.Add(tag, tagFloat);
                            }
                            else
                            {
                                failure = true;
                                LogLine($"Tag parsing CRITICAL: Failed to interpret {i} as a float.");
                            }
                        }
                        odd = !odd;
                    }
                    if(!failure)
                        return result;
                    LogLine("Tag parsing CRITICAL: There are lots of values you put here, but no separator ',,,' was found! Sequential tag-float input parsing failed as well!");
                    return null;
                }
            }
        }

        static string OrderToString(NSFWOrder order)
        {
            switch(order)
            {
                case NSFWOrder.ASCENDING_LIKE:
                    return "Ascending by like rating";
                case NSFWOrder.DESCENDING_LIKE:
                    return "Descending by like rating";
                case NSFWOrder.RANDOM:
                    return "Randomized";
                case NSFWOrder.NO_SORT:
                    return "Don't sort";
            }
            return "Unknown";
        }

        static List<string> ParseInput(string input)
        {
            return input.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        static void Log(object item)
        {
            Console.Write($"[{DateTime.UtcNow.ToString("s")}]" + item);
        }

        static void LogLine(object item)
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString("s")}]" + item);
        }
    }
}
