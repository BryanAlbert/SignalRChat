namespace SignalRConsole
{
	public class Arithmetic
	{
		public Arithmetic()
		{
		}

		public Arithmetic(string handle, string email)
		{
			Handle = handle;
			Email = email;
		}

		public Arithmetic(User user) : this(user.Handle, user.Email)
		{
			Id = user.Id;
			Name = user.Name;
			Color = user.Color;
			Created = user.Created;
			Modified = user.Modified;
		}


		public string Id { get; set; }
		public string Email { get; set; }
		public string Name { get; set; }
		public string Handle { get; set; }
		public string Color { get; set; }
		public string Created { get; set; }
		public string Modified { get; set; }
	}
}