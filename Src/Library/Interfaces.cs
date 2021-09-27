namespace FastEndpoints
{
    public interface IRequest<TEntity>
    {
        TEntity ToEntity();
    }

    public interface IResponse<TEntity>
    {
        void FromEntity(TEntity entity);
    }

    public interface IEndpoint { }
}
