using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoYearsCacheTests
{
    private const long UserId = 1;

    private readonly PhotoYearsCache _cache = new();
    private int _loads;

    [Fact]
    public async Task GetOrLoadAsync_ShouldReadTheYearsOnce_AndSortThem()
    {
        // Act
        var first = await _cache.GetOrLoadAsync(UserId, Load(2019, 2016));
        var second = await _cache.GetOrLoadAsync(UserId, Load(2020));

        // Assert
        Assert.Equal([2016, 2019], first);
        Assert.Equal([2016, 2019], second);
        Assert.Equal(1, _loads);
    }

    [Fact]
    public async Task AddYears_ShouldAddToTheCachedYears()
    {
        // Arrange
        await _cache.GetOrLoadAsync(UserId, Load(2016));

        // Act
        _cache.AddYears(UserId, [2021, 2016]);

        // Assert
        Assert.Equal([2016, 2021], await _cache.GetOrLoadAsync(UserId, Load()));
        Assert.Equal(1, _loads);
    }

    [Fact]
    public async Task Invalidate_ShouldReadTheYearsAgain()
    {
        // Arrange
        await _cache.GetOrLoadAsync(UserId, Load(2016, 2019));

        // Act
        _cache.Invalidate(UserId);

        // Assert
        Assert.Equal([2019], await _cache.GetOrLoadAsync(UserId, Load(2019)));
        Assert.Equal(2, _loads);
    }

    [Fact]
    public async Task GetOrLoadAsync_ShouldNotKeepYearsRead_WhilePhotosWereAdded()
    {
        // Arrange: photos are saved while the years are being read, which may have missed them
        var years = await _cache.GetOrLoadAsync(UserId, () =>
        {
            _cache.AddYears(UserId, [2021]);
            return Task.FromResult<IEnumerable<int>>([2016]);
        });

        // Act
        var readAgain = await _cache.GetOrLoadAsync(UserId, Load(2016, 2021));

        // Assert
        Assert.Equal([2016], years);
        Assert.Equal([2016, 2021], readAgain);
    }

    [Fact]
    public async Task Years_ShouldBeKeptPerUser()
    {
        // Arrange
        await _cache.GetOrLoadAsync(UserId, Load(2016));

        // Act
        _cache.Invalidate(2);

        // Assert
        Assert.Equal([2016], await _cache.GetOrLoadAsync(UserId, Load()));
        Assert.Equal(1, _loads);
    }

    private Func<Task<IEnumerable<int>>> Load(params int[] years)
    {
        return () =>
        {
            _loads++;
            return Task.FromResult<IEnumerable<int>>(years);
        };
    }
}
