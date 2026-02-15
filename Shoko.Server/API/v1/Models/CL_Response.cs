namespace Shoko.Server.API.v1.Models;

public class CL_Response<T> : CL_Response
{
    public T Result { get; set; }

}
public class CL_Response
{
    public string ErrorMessage { get; set; }
}
