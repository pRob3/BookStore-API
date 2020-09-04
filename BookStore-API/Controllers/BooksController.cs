using AutoMapper;
using BookStore_API.Contracts;
using BookStore_API.Data;
using BookStore_API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookStore_API.Controllers
{
    /// <summary>
    /// Interacts with the books Table
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BooksController : ControllerBase
    {
        private readonly IBookRepository _bookRepository;
        private readonly ILoggerService _logger;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public BooksController(IBookRepository bookRepository, ILoggerService logger, IMapper mapper, IWebHostEnvironment env, IConfiguration config)
        {
            _bookRepository = bookRepository;
            _logger = logger;
            _mapper = mapper;
            _env = env;
            _config = config;

            // Create book image folder if it don't exists
            CreateImageFolder();
        }

        private string CreateImageFolder()
        {
            // Create folder
            System.IO.DirectoryInfo di = System.IO.Directory.
                CreateDirectory($"{_env.ContentRootPath}{_config.GetValue<string>("UploadFolders:Books")}");

            return di.ToString();
        }

        private string GetImagePath(string fileName) 
            => ($"{_env.ContentRootPath}{_config.GetValue<string>("UploadFolders:Books")}{fileName}");


        /// <summary>
        /// Get all Books
        /// </summary>
        /// <returns>A List of Books</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBooks()
        {
            var location = GetControllerActionNames();
            try
            {

                _logger.LogInfo($"{ location }: Attempted Call");

                var books = await _bookRepository.FindAll();
                var response = _mapper.Map<IList<BookDTO>>(books);

                foreach (var item in response)
                {
                    if (!string.IsNullOrEmpty(item.Image))
                    {
                        var imgPath = GetImagePath(item.Image);
                        if (System.IO.File.Exists(imgPath))
                        {
                            byte[] imgBytes = System.IO.File.ReadAllBytes(imgPath);
                            item.File = Convert.ToBase64String(imgBytes);
                        }
                    }
                }

                _logger.LogInfo($"{ location }: Successful Call");
                return Ok(response);
            }
            catch (Exception e)
            {
                return InternalError($"{ location }: {e.Message} - {e.InnerException}");
            }
        }

        /// <summary>
        /// Get's a Book by Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>A Book Record</returns>
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBook(int id)
        {
            var location = GetControllerActionNames();
            try
            {
                _logger.LogInfo($"{location}: Attempted Call for id: {id}");
                var book = await _bookRepository.FindById(id);
                if (book == null)
                {
                    _logger.LogWarn($"{location}: Failed to retrieve record with id: {id}");
                    return NotFound();
                }
                var response = _mapper.Map<BookDTO>(book);

                // Does image exists
                if (!string.IsNullOrEmpty(response.Image))
                {
                    var imgPath = GetImagePath(book.Image);
                    if (System.IO.File.Exists(imgPath))
                    {
                        byte[] imgBytes = System.IO.File.ReadAllBytes(imgPath);
                        response.File = Convert.ToBase64String(imgBytes);
                    }
                }

                _logger.LogInfo($"{location}: Successfully got record with id: {id}");
                return Ok(response);
            }
            catch (Exception e)
            {
                return InternalError($"{location}: {e.Message} - {e.InnerException}");
            }
        }

        /// <summary>
        /// Creates a new book
        /// </summary>
        /// <param name="bookDTO"></param>
        /// <returns>Book Object</returns>
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] BookCreateDTO bookDTO)
        {
            var location = GetControllerActionNames();
            try
            {
                _logger.LogInfo($"{ location }: Create Attempted");
                if (bookDTO == null)
                {
                    _logger.LogWarn($"{ location }: Empty request was submitted");
                    return BadRequest(ModelState);
                }
                if (!ModelState.IsValid)
                {
                    _logger.LogWarn($"{ location }: Data was incomplete");
                    return BadRequest(ModelState);
                }

                var book = _mapper.Map<Book>(bookDTO);
                var isSuccess = await _bookRepository.Create(book);

                if (!isSuccess)
                {
                    return InternalError($"{location}: Creation failed");
                }

                // Save image
                if (!string.IsNullOrEmpty(bookDTO.File))
                {
                    // Upload folder
                    var imgPath = GetImagePath(bookDTO.Image);
                    // Get image
                    byte[] imageBytes = Convert.FromBase64String(bookDTO.File);
                    // Save image
                    System.IO.File.WriteAllBytes(imgPath, imageBytes);
                }

                _logger.LogInfo($"{location}: Creation was successful");
                return Created("Create", new { book });

            }
            catch (Exception e)
            {
                return InternalError($"{location}: {e.Message} - {e.InnerException}");
            }
        }


        /// <summary>
        /// Update an book
        /// </summary>
        /// <param name="id"></param>
        /// <param name="bookDTO"></param>
        /// <returns></returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(int id, [FromBody] BookUpdateDTO bookDTO)
        {
            var location = GetControllerActionNames();
            try
            {
                _logger.LogInfo($"{location}: Update Attempted on record with id: {id}");

                if (id < 1 || bookDTO == null || id != bookDTO.Id)
                {
                    _logger.LogWarn($"{location}: Update failed with bada data - id: {id}");
                    return BadRequest();
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarn($"{location}: Data was incomplete - id: {id}");
                    return BadRequest(ModelState);
                }

                var isExists = await _bookRepository.isExists(id);
                if (!isExists)
                {
                    _logger.LogWarn($"{location}: Failed to retrive record with id: {id}");
                    return NotFound();
                }
                var oldImage = await _bookRepository.GetImageFileName(id);

                var book = _mapper.Map<Book>(bookDTO);
                var isSuccess = await _bookRepository.Update(book);

                if (!isSuccess)
                {
                    return InternalError($"{location}: Update failed for record with id: {id}");
                }

                // Remove old image if changed or removed
                if (!bookDTO.Image.Equals(oldImage))
                {
                    if (System.IO.File.Exists(GetImagePath(oldImage)))
                    {
                        System.IO.File.Delete(GetImagePath(oldImage));
                    }
                }

                // Update image
                if (!string.IsNullOrEmpty(bookDTO.File))
                {
                    byte[] imgBytes = Convert.FromBase64String(bookDTO.File);
                    System.IO.File.WriteAllBytes(GetImagePath(bookDTO.Image), imgBytes);
                }

                _logger.LogInfo($"{location}: with id: {id} successfully updated");
                return NoContent();

            }
            catch (Exception e)
            {
                return InternalError($"{location}: {e.Message} - {e.InnerException}");
            }
        }

        /// <summary>
        /// Removes a book by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(int id)
        {
            var location = GetControllerActionNames();
            try
            {
                _logger.LogInfo($"{location}: Delete Attempted on record with id: {id}");

                if (id < 1)
                {
                    _logger.LogWarn($"{location}: Delete failed");
                    return BadRequest();
                }

                var isExists = await _bookRepository.isExists(id);
                if (!isExists)
                {
                    _logger.LogWarn($"{location}: Delete with id: {id} was not found");
                    return NotFound();
                }

                var book = await _bookRepository.FindById(id);
                var isSuccess = await _bookRepository.Delete(book);
                if (!isSuccess)
                {
                    return InternalError($"{location}: Delete Failed on record {id}");
                }

                _logger.LogInfo($"{location}: Record with id: {id} successfully deleted");
                return NoContent();
            }
            catch (Exception e)
            {
                return InternalError($"{location}: {e.Message} - {e.InnerException}");
            }
        }



        private string GetControllerActionNames()
        {
            var controller = ControllerContext.ActionDescriptor.ControllerName;
            var action = ControllerContext.ActionDescriptor.ActionName;

            return ($"{controller} / {action}");
        }

        private ObjectResult InternalError(string message)
        {
            _logger.LogError(message);

            return StatusCode(500, "Something went wrong. Please contact the Administration");
        }
    }
}