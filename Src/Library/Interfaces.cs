namespace FastEndpoints
{
    /// <summary>
    /// use this interface to add functionality to easily map a request dto to a given entity
    /// </summary>
    /// <typeparam name="TEntity">the type of the entity to map to</typeparam>
    public interface IRequest<TEntity>
    {
        /// <summary>
        /// the method that maps a request dto to an entity
        /// </summary>
        TEntity ToEntity();
    }

    /// <summary>
    /// use this interface to populate a response dto from a given entity
    /// </summary>
    /// <typeparam name="TEntity">the type of the entity to populate values from</typeparam>
    public interface IResponse<TEntity>
    {
        /// <summary>
        /// the method that populates response dto from an entity
        /// </summary>
        /// <param name="entity">the type of the entity to populate from</param>
        void FromEntity(TEntity entity);
    }

    internal interface IEndpoint { }
}
