using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IPersonRepository : IRepository<Person>
{
    Task<Person?> GetByNameAsync(string name);
    Task<IReadOnlyList<Person>> GetPeopleWithPhotoCountAsync();
    Task<IReadOnlyList<Person>> GetPeopleWithFaceCountAsync();
    Task<Person?> GetWithFacesAsync(int personId);
    Task<int> GetPersonCountAsync();
    Task<IReadOnlyList<int>> GetFaceIdsByPersonIdAsync(int personId);
    Task<Person?> GetByFaceIdAsync(int faceId);
}
