﻿using Microsoft.AspNetCore.Mvc;

namespace SmartaCam
{
    [ApiController]
    [Route("api/{action}")]
    public class TakeController : ControllerBase
    {
        private ITakeRepository _takeRepository;

		public TakeController(ITakeRepository takeRepo)
        {
            _takeRepository = takeRepo;

        }
        // [HttpGet("{id:int}")]
        [HttpGet("/{id:int}")]
        public async Task<ActionResult<Take>> GetTakeById(int id)
        {
            return Ok(await _takeRepository.GetTakeByIdAsync(id));

        }
		//[HttpGet]
		//public async Task<ActionResult<DateTime>> GetLatestTakeDate()
		//{
		//	return Ok(await _takeRepository.GetLastTakeDateAsync());
		//}
		[HttpGet]
        public async Task<ActionResult<List<Take>>> GetAllTakes()
        {
            return Ok(await _takeRepository.GetAllTakesAsync());

        }
        [HttpPost]
        public async Task<ActionResult<Take>> AddTake(Take newTake)
        {
            // return Ok(await _takeRepository.AddTakeAsync(newTake));
            await _takeRepository.AddTakeAsync(newTake);
            await _takeRepository.SaveChangesAsync();
            return CreatedAtAction(nameof(GetTakeById), new { id = newTake.Id }, newTake);

        }

    }
    
}