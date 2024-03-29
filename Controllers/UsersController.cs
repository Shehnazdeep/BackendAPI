using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Models.Entities;
using API.Models.Helpers;
using API.Models.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Responses;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {

        private Database _db = new Database();

        //create user
        [HttpPost]

        public async Task<IActionResult> CreateUser(User user)

        {

            //todo: add validation
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if the email is unique by compairing the emails of user in the database with the enterd email
            if (_db.Users.Any(u => u.Email == user.Email))
            {
                return BadRequest(new
                {

                    Message = "Email is not unique"
                });
            }



            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();
            return Created("", user);

        }

        // POST /api/users/{id}/image
        [HttpPost("{userId}/image")]
        public async Task<IActionResult> AddImageToUser([FromRoute] Guid userId, [FromBody] Image image)
        {


            // if (string.IsNullOrEmpty(url))
            // {
            //     return BadRequest(new
            //     {
            //         Message = "URL cannot be null or empty."
            //     });
            // }
            var user = await _db.Users.FindAsync(userId);


            if (user == null)
                return BadRequest(new
                {

                    Message = "Provided Id seems invalid!"
                });

            image.User = user;
            image.PostingDate = DateTime.Now;


            var tagNames = ImageHelper.GetTags(image.Url).ToList();
            // Convert tag names to Tag objects
            var tags = tagNames.Select(tagName => new Tag
            {
                Id = Guid.NewGuid(), // Generate a new Guid for the tag
                Text = tagName,
                Images = new List<Image> { image } // Link the image to the tag
            }).ToList();

            _db.Tags.AddRange(tags);

            image.Tags = tags;

            //  user.Images ??= new List<Image>();
            user.Images.Add(image);
            _db.Images.Add(image);
            await _db.SaveChangesAsync();

            var last10Images = user.Images.OrderByDescending(i => i.PostingDate).Take(10).ToList();
            Console.WriteLine($"Number of images in last10Images: {last10Images.Count}");
            var userDto = new UserDTO()
            {
                //advantage of using Dto classes is to promote consistency. 
                //we can also use anonymus onjects but that is not good for consistnecy
                Id = user.Id,
                Username = user.Name,
                Email = user.Email,
                ImagesUrls = last10Images.Select(i => i.Url).ToList()
            };

            return Ok(userDto);


        }


        // GET /api/users/{id}

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserById(Guid userId)
        {

            if (userId == Guid.Empty)
            {
                return BadRequest(new
                {
                    Message = "Invalid ID format."
                });
            }

            // Retrieve the image from the database
            var user = await _db.Users.Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == userId);

            //  var user = await _db.Users.Where(image)
            // Check if the image with the provided ID exists
            if (user == null)
            {
                return NotFound(new
                {
                    Message = "User not found."
                });
            }
            if (user.Images == null)
            {
                return BadRequest(new
                {
                    Message = "No Images associated with user"
                });
            }


            var userDetails = new
            {
                Id = user.Id,

                UserName = user.Name,
                email = user.Email,
                ImagesUrls = user.Images.Select(x => x.Url).ToList(),
            };

            return Ok(userDetails);

        }

        //GET /api/users/{id}/images

        [HttpGet("{userId}/images")]
        public async Task<IActionResult> GetImagesForEachUser(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest(new
                {
                    Message = "Invalid ID format."
                });
            }

            // Retrieve the image from the database
            var user = await _db.Users.Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == userId);

            //  var user = await _db.Users.Where(image)
            // Check if the image with the provided ID exists
            if (user == null)
            {
                return NotFound(new
                {
                    Message = "User not found."
                });
            }

            if (user.Images == null)
            {
                return BadRequest(new
                {
                    Message = "No Images associated with user"
                });
            }


            var imageDtos = user.Images.Select(image => new ImageDTO
            {
                Id = image.Id,
                Url = image.Url,


            }).ToList();


            var response = new PageResponse<ImageDTO>(imageDtos);
            //   var totalRecords = _db.People.CountAsync();
            response.Meta.Add("TotalPages", 10);
            response.Meta.Add("TotalRecords", 200);
            var links = LinksGenerator.GenerateLinks("/api/Images", 1, 200, 10);

            response.Links = links;
            return Ok(response);


        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveUser([FromRoute] Guid id)
        {
            var user = await _db.Users
                .Include(u => u.Images)
                    .ThenInclude(i => i.Tags)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            foreach (var image in user.Images)
            {
                // Remove tags associated with the image
                _db.Tags.RemoveRange(image.Tags);
            }

            // Remove images associated with the user
            _db.Images.RemoveRange(user.Images);

            // Remove the user
            _db.Users.Remove(user);

            await _db.SaveChangesAsync();

            return Ok(new { Message = "User, images, and  tags removed successfully" });
        }


    }
}