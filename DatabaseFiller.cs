using DataLoader.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace DataLoader
{
    internal class DatabaseFiller
    {
        static List<string> Messages = new List<string>();
        static string csvFilePath = "random_messages.csv";
        static Random random = new Random();
        static byte[] photoBytes;

        static List<string> adjectives = new List<string>
        {
             "Happy", "Curious", "Creative", "Brave", "Bold", "Clever", "Chill", "Fun",
            "Friendly", "Brainy", "Adventurous", "Playful", "Dreamy", "Energetic", "Smart",
            "Witty", "Social", "Mighty", "Techy", "Artistic", "Mindful", "Thoughtful",
            "Innovative", "Passionate", "Curious", "Dynamic", "Joyful", "Cheerful", "Lively"
        };

        static List<string> nouns = new List<string>
        {
           "Hive", "Circle", "Club", "Crew", "Lounge", "Hub", "Guild", "Pack",
            "Network", "Den", "Team", "Society", "Bunch", "Forum", "Squad", "Nest",
            "Zone", "Collective", "Alliance", "Tribe", "Corner", "Community", "Gang",
            "Society", "Circle", "Group", "Association", "Fellowship", "League"
        };


        public async static Task<bool> FillFollowers(int minFollowingCount, int maxFollowingCount)
        {
            try
            {
                var db = new DatabaseContext();
                var users = db.Users.ToList();
                Random random = new Random();

                // List to store new followers
                var newFollowers = new List<Follower>();

                foreach (var user in users)
                {
                    // Determine how many people this user will follow
                    int followerCount = random.Next(minFollowingCount, maxFollowingCount + 1);

                    // Pick random users to follow
                    var possibleFollowed = users
                        .Where(u => u.UserId != user.UserId)        // exclude self
                        .OrderBy(u => random.Next())        // shuffle
                        .Take(followerCount)
                        .ToList();

                    foreach (var followed in possibleFollowed)
                    {
                        var startDate = new[] { user.CreatedAt ?? DateTime.UtcNow, followed.CreatedAt ?? DateTime.UtcNow }.Min();
                        newFollowers.Add(new Follower
                        {
                            FollowerId = user.UserId,
                            FollowedId = followed.UserId,
                            FollowedAt = RandomDate(startDate, DateTime.Now),
                            Status = "active"
                        });
                    }

                    db.Followers.AddRange(newFollowers);
                    newFollowers.Clear();
                    // Save changes
                    db.SaveChanges();
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while adding followers: {ex.Message}");
                return false;
            }

            return true;
        }



        public static DateTime RandomDate(DateTime start, DateTime end)
        {
            var range = (end - start).TotalSeconds;
            var randomSeconds = random.NextDouble() * range;
            return start.AddSeconds(randomSeconds);
        }


        public async static Task<bool> CreateGroups(int groupCount, int minMembers, int maxMembers)
        {
            try
            {
                var db = new DatabaseContext();
                var users = db.Users.ToList();
                Random random = new Random();
                for (int i = 0; i < groupCount; i++)
                {
                    var group = new Group
                    {
                        Name = $"{adjectives[random.Next(adjectives.Count)]} {nouns[random.Next(nouns.Count)]}",
                        CreatedAt = RandomDate(users.Min(u => u.CreatedAt) ?? DateTime.Now, DateTime.UtcNow),
                    };
                    db.Groups.Add(group);
                    db.SaveChanges();

                    // Add random members to the group
                    int memberCount = random.Next(minMembers, maxMembers); // Between 5 and 20 members
                    var groupMembers = users
                        .OrderBy(u => random.Next())
                        .Where(u => u.CreatedAt > group.CreatedAt)
                        .Take(memberCount)
                        .Select(u => new GroupMember
                        {
                            GroupId = group.GroupId,
                            UserId = u.UserId,
                            JoinedAt = RandomDate(u.CreatedAt ?? DateTime.Now, DateTime.UtcNow)
                        }).ToList();
                    db.GroupMembers.AddRange(groupMembers);
                    if (groupMembers.Count == 0)
                    {
                        db.Groups.Remove(group);
                        i--;
                    }
                    else
                    {
                        group.CreatedBy = groupMembers[random.Next(groupMembers.Count)].UserId;
                    }
                    db.SaveChanges();
                }
            }
            catch (Exception _)
            {
                return false;
            }
            return true;
        }


        public async static Task<bool> CreateMessages(int messageCount)
        {
            try
            {
                var db = new DatabaseContext();
                var users = db.Users.ToList();
                for (int i = 0; i < messageCount; i++)
                {
                    var sender = users[random.Next(users.Count)];
                    var Recipients = users
                        .Where(u => u.UserId != sender.UserId)        // exclude self
                        .OrderBy(u => random.Next())        // shuffle
                        .Take(1)
                        .ToList();
                    var recipient = Recipients[0];
                    var messageDate = new[] { sender.CreatedAt ?? DateTime.UtcNow, recipient.CreatedAt ?? DateTime.UtcNow }.Min();
                    string message = GetRandomMessage();
                    db.Messages.Add(
                        new Message() { Sender = sender, Receiver = recipient, SentAt = messageDate, Content = message, StatusId = random.Next(1, 6) });

                    db.SaveChanges();
                }
            }
            catch (Exception _)
            {
                return false;
            }
            return true;
        }
        public async static Task<bool> CreateGroupMessages(int messageCount)
        {
            try
            {
                var db = new DatabaseContext();
                var groups = db.Groups.ToList();

                for (int i = 0; i < messageCount; i++)
                {
                    var group = groups[random.Next(groups.Count)];
                    var groupMembers = db.GroupMembers.Where(gm => gm.GroupId == group.GroupId).ToList();
                    if (groupMembers.Count == 0)
                    {
                        i--;
                        continue;
                    }
                    var member = groupMembers[random.Next(groupMembers.Count)];
                    var message = GetRandomMessage();
                    db.GroupMessages.Add(new GroupMessage
                    {
                        Group = group,
                        Content = message,
                        SenderId = member.UserId,
                        SentAt = RandomDate(group.CreatedAt ?? DateTime.Now, DateTime.Now),
                        StatusId = 3
                    });

                    db.SaveChanges();
                }
            }
            catch (Exception _)
            {
                return false;
            }
            return true;
        }

        public static string GetRandomMessage()
        {
            if (Messages.IsNullOrEmpty())
            {
                Messages = File.ReadAllLines(csvFilePath)
                          .Skip(1) // skip header
                          .Where(line => !string.IsNullOrWhiteSpace(line))
                          .ToList();
            }
            return Messages[random.Next(0, Messages.Count)];
        }

        public async static Task<bool> CreateCloseFriends(int userCount, int minCount, int maxCount)
        {
            try
            {
                var db = new DatabaseContext();
                var users = db.Users.ToList();
                Random random = new Random();

                // List to store new followers
                var newFollowers = new List<Follower>();

                for (int i = 0; i < userCount; i++)
                {
                    {
                        // Determine how many people this user will follow
                        var user = users[i];
                        int closeCount = random.Next(minCount, maxCount + 1);
                        if (closeCount == 0)
                        {
                            i--;
                            continue;
                        }

                        var existingFriendIds = db.CloseFriends
                             .Where(cf => cf.OwnerId == user.UserId)
                             .Select(cf => cf.FriendId)
                             .ToHashSet();

                        // Pick possible new friends
                        var possibleClose = users
                            .Where(u => u.UserId != user.UserId && !existingFriendIds.Contains(u.UserId))
                            .OrderBy(u => random.Next())
                            .Take(closeCount)
                            .ToList();

                        foreach (var close in possibleClose)
                        {
                            var startDate = new[] { user.CreatedAt ?? DateTime.UtcNow, close.CreatedAt ?? DateTime.UtcNow }.Min();
                            db.CloseFriends.Add(new CloseFriend
                            {
                                OwnerId = user.UserId,
                                FriendId = close.UserId,
                                AddedAt = RandomDate(startDate, DateTime.Now)
                            });
                            db.SaveChanges();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while adding followers: {ex.Message}");
                return false;
            }

            return true;
        }

        public static byte[] GetProfilePhoto()
        {
            if (photoBytes == null)
            {
                string photoPath = "profile.jpg";


                if (!File.Exists(photoPath))
                {
                    Console.WriteLine($"Photo not found at {photoPath}");
                    return null;
                }
                photoBytes = File.ReadAllBytes(photoPath);
            }

            return photoBytes;
        }

        public static async Task<bool> CreateProfilePictures()
        {
            try
            {
                var db = new DatabaseContext();
                List<User> users = new();
                do
                {
                    //users = db.Users.Take(500)/*.Where(u => u.ProfilePhoto == null || u.ProfilePhoto.Length == 0)*/.ToList();
                    users = db.Users
    .FromSqlRaw("SELECT * FROM USERS WHERE PROFILE_PHOTO IS NULL OR dbms_lob.getlength(PROFILE_PHOTO) = 0 FETCH FIRST 500 ROWS ONLY")
    .ToList();
                    var photoBytes = GetProfilePhoto();
                    if (photoBytes == null)
                    {
                        return false;
                    }
                    foreach (var user in users)
                    {
                        //user.ProfilePhoto = photoBytes;
                        //db.SaveChanges();
                    }
                } while (users.Any());
            }
            catch (Exception _)
            {
                return false;
            }
            return true;
        }

        public static async Task<bool> CreatePosts()
        {
            try
            {
                var db = new DatabaseContext();
                var users = db.Users.Take(100).ToList();
                for (int i = 1; i < 100; i++)
                {
                    var user = users[random.Next(users.Count)];
                    var postDate = RandomDate(user.CreatedAt ?? DateTime.Now, DateTime.Now);
                    string message = GetRandomMessage();
                    db.Posts.Add(
                        new Post() { PostId = i, UserId = user.UserId, CreatedAt = postDate, PostComment = message, Photo = GetProfilePhoto() });
                    db.SaveChanges();
                }
            }
            catch (Exception _)
            {
                return false;
            }
            return true;
        }

        public static async Task<bool> CreateLikes(int likeCount)
        {
            try
            {
                var db = new DatabaseContext();
                var posts = db.Posts.ToList();
                for (int i = 0; i < likeCount; i++)
                {
                    var users = db.Users
                        .OrderBy(u => Guid.NewGuid())
                        .Take(1)
                        .ToList();

                    var user = users[0];
                    var post = posts[random.Next(posts.Count)];
                    var likeDate = new[] { user.CreatedAt ?? DateTime.UtcNow, post.CreatedAt ?? DateTime.UtcNow }.Min();
                    db.Likes.Add(
                        new Like() { UserId = user.UserId, PostId = post.PostId, CreatedAt = RandomDate(likeDate, DateTime.Now) });
                    db.SaveChanges();
                }
            }
            catch (Exception _)
            {
                return false;
            }
            return true;
        }

        public static async Task<bool> CreateComments(int commentCount)
        {
            try
            {
                var db = new DatabaseContext();
                var posts = db.Posts.ToList();
                for (int i = 0; i < commentCount; i++)
                {
                    var users = db.Users
                        .OrderBy(u => Guid.NewGuid())
                        .Take(1)
                        .ToList();

                    var user = users[0];
                    var post = posts[random.Next(posts.Count)];
                    var commentDate = new[] { user.CreatedAt ?? DateTime.UtcNow, post.CreatedAt ?? DateTime.UtcNow }.Min();
                    db.Comments.Add(
                        new Comment() { UserId = user.UserId, PostId = post.PostId, CreatedAt = RandomDate(commentDate, DateTime.Now), Content = GetRandomMessage() });
                    db.SaveChanges();
                }
            }
            catch (Exception _)
            {
                return false;
            }
            return true;
        }
    }
}