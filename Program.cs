
using DataLoader;
using DataLoader.Models;
using Microsoft.EntityFrameworkCore;



//DatabaseFiller.CreateCloseFriends(500, 0,5);
DatabaseFiller.CreateProfilePictures();
DatabaseFiller.CreateMessages(7000);
DatabaseFiller.CreateGroupMessages(10000);