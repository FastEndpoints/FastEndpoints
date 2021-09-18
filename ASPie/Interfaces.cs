namespace ASPie
{
    public interface IRequest { }

    public interface IRequest<TEntity> : IRequest
    {
        TEntity ToEntity();
    }
    public interface IResponse { }

    public interface IResponse<TEntity> : IResponse
    {
        void FromEntity(TEntity entity);
    }

    public interface IHandler { }
}
