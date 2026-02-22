using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using MySql.Data;
using MySql.Data.MySqlClient;
using System.Data;

namespace ProjectTemplate
{
	[WebService(Namespace = "http://tempuri.org/")]
	[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
	[System.ComponentModel.ToolboxItem(false)]
	[System.Web.Script.Services.ScriptService]

	public class ProjectServices : System.Web.Services.WebService
	{
		////////////////////////////////////////////////////////////////////////
		///replace the values of these variables with your database credentials
		////////////////////////////////////////////////////////////////////////
		private string dbID = "root";
		private string dbPass = "Emerald2004!";
		private string dbName = "group11_database";
		private string dbServer = "localhost";
		private string dbPort = "3306";
		////////////////////////////////////////////////////////////////////////
		
		////////////////////////////////////////////////////////////////////////
		///call this method anywhere that you need the connection string!
		////////////////////////////////////////////////////////////////////////
		private string getConString() {
			return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName+"; UID=" + dbID + "; PASSWORD=" + dbPass;
		}
		////////////////////////////////////////////////////////////////////////



				/////////////////////////////////////////////////////////////////////////
		//EXISTING METHODS (Keep your existing methods from Sprint 1 here)
		/////////////////////////////////////////////////////////////////////////
		[WebMethod(EnableSession = true)]
		public string TestConnection()
		{
			try
			{
				string testQuery = "SELECT 1";
				MySqlConnection con = new MySqlConnection(getConString());
				MySqlCommand cmd = new MySqlCommand(testQuery, con);
				MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
				DataTable table = new DataTable();
				adapter.Fill(table);
				return "Success! Connected to " + dbName;
			}
			catch (Exception e)
			{
				return "Connection failed. Error: " + e.Message;
			}
		}



		/////////////////////////////////////////////////////////////////////////
		//SPRINT 2 - ESSAY QUESTIONS FEATURE
		/////////////////////////////////////////////////////////////////////////

		/// <summary>
		/// Get all active essay questions with user's submission status
		/// </summary>
		[WebMethod(EnableSession = true)]
		public EssayQuestion[] GetEssayQuestions(int userId)
		{
			try
			{
				List<EssayQuestion> questions = new List<EssayQuestion>();

				string query = @"
					SELECT 
						eq.id,
						eq.title,
						eq.question_text,
						eq.points,
						eq.created_at,
						eq.active,
						es.id as submission_id,
						es.submitted_at
					FROM essay_questions eq
					LEFT JOIN essay_submissions es 
						ON eq.id = es.question_id 
						AND es.user_id = @userId
					WHERE eq.active = 1
					ORDER BY eq.created_at DESC";

				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();
					MySqlCommand cmd = new MySqlCommand(query, con);
					cmd.Parameters.AddWithValue("@userId", userId);

					using (MySqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							EssayQuestion question = new EssayQuestion
							{
								Id = reader.GetInt32("id"),
								Title = reader.GetString("title"),
								QuestionText = reader.GetString("question_text"),
								Points = reader.GetInt32("points"),
								CreatedAt = reader.GetDateTime("created_at"),
								Active = reader.GetBoolean("active"),
								HasSubmitted = !reader.IsDBNull(reader.GetOrdinal("submission_id")),
								SubmittedAt = reader.IsDBNull(reader.GetOrdinal("submitted_at")) 
									? (DateTime?)null 
									: reader.GetDateTime("submitted_at")
							};
							questions.Add(question);
						}
					}
				}

				return questions.ToArray();
			}
			catch (Exception e)
			{
				throw new Exception("Error fetching essay questions: " + e.Message);
			}
		}

		/// <summary>
		/// Get details of a single essay question
		/// </summary>
		[WebMethod(EnableSession = true)]
		public EssayQuestionDetail GetEssayQuestionDetail(int questionId, int userId)
		{
			try
			{
				EssayQuestionDetail detail = null;

				string query = @"
					SELECT 
						eq.id,
						eq.title,
						eq.question_text,
						eq.points,
						eq.created_at,
						eq.active,
						es.id as submission_id,
						es.answer_text,
						es.submitted_at,
						es.points_awarded
					FROM essay_questions eq
					LEFT JOIN essay_submissions es 
						ON eq.id = es.question_id 
						AND es.user_id = @userId
					WHERE eq.id = @questionId AND eq.active = 1";

				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();
					MySqlCommand cmd = new MySqlCommand(query, con);
					cmd.Parameters.AddWithValue("@questionId", questionId);
					cmd.Parameters.AddWithValue("@userId", userId);

					using (MySqlDataReader reader = cmd.ExecuteReader())
					{
						if (reader.Read())
						{
							detail = new EssayQuestionDetail
							{
								Id = reader.GetInt32("id"),
								Title = reader.GetString("title"),
								QuestionText = reader.GetString("question_text"),
								Points = reader.GetInt32("points"),
								CreatedAt = reader.GetDateTime("created_at"),
								Active = reader.GetBoolean("active"),
								HasSubmitted = !reader.IsDBNull(reader.GetOrdinal("submission_id")),
								SubmittedAnswer = reader.IsDBNull(reader.GetOrdinal("answer_text")) 
									? null 
									: reader.GetString("answer_text"),
								SubmittedAt = reader.IsDBNull(reader.GetOrdinal("submitted_at")) 
									? (DateTime?)null 
									: reader.GetDateTime("submitted_at"),
								PointsAwarded = reader.IsDBNull(reader.GetOrdinal("points_awarded")) 
									? (int?)null 
									: reader.GetInt32("points_awarded")
							};
						}
					}
				}

				if (detail == null)
				{
					throw new Exception("Question not found or inactive");
				}

				return detail;
			}
			catch (Exception e)
			{
				throw new Exception("Error fetching question detail: " + e.Message);
			}
		}

		/// <summary>
		/// Submit an answer to an essay question
		/// </summary>
		[WebMethod(EnableSession = true)]
		public SubmissionResult SubmitEssayAnswer(int userId, int questionId, string answerText)
		{
			try
			{
				// Validate answer length
				if (string.IsNullOrWhiteSpace(answerText) || answerText.Length < 50)
				{
					return new SubmissionResult
					{
						Success = false,
						Message = "Answer must be at least 50 characters long",
						PointsAwarded = 0
					};
				}

				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();

					// Check if question exists and is active
					string checkQuestionQuery = "SELECT points FROM essay_questions WHERE id = @questionId AND active = 1";
					MySqlCommand checkCmd = new MySqlCommand(checkQuestionQuery, con);
					checkCmd.Parameters.AddWithValue("@questionId", questionId);
					
					object result = checkCmd.ExecuteScalar();
					if (result == null)
					{
						return new SubmissionResult
						{
							Success = false,
							Message = "Question not found or inactive",
							PointsAwarded = 0
						};
					}

					int points = Convert.ToInt32(result);

					// Check if user already submitted
					string checkSubmissionQuery = "SELECT COUNT(*) FROM essay_submissions WHERE user_id = @userId AND question_id = @questionId";
					MySqlCommand checkSubmissionCmd = new MySqlCommand(checkSubmissionQuery, con);
					checkSubmissionCmd.Parameters.AddWithValue("@userId", userId);
					checkSubmissionCmd.Parameters.AddWithValue("@questionId", questionId);
					
					int submissionCount = Convert.ToInt32(checkSubmissionCmd.ExecuteScalar());
					if (submissionCount > 0)
					{
						return new SubmissionResult
						{
							Success = false,
							Message = "You have already submitted an answer to this question",
							PointsAwarded = 0
						};
					}

					// Start transaction
					MySqlTransaction transaction = con.BeginTransaction();

					try
					{
						// Insert submission
						string insertQuery = @"
							INSERT INTO essay_submissions (user_id, question_id, answer_text, points_awarded)
							VALUES (@userId, @questionId, @answerText, @points)";

						MySqlCommand insertCmd = new MySqlCommand(insertQuery, con, transaction);
						insertCmd.Parameters.AddWithValue("@userId", userId);
						insertCmd.Parameters.AddWithValue("@questionId", questionId);
						insertCmd.Parameters.AddWithValue("@answerText", answerText);
						insertCmd.Parameters.AddWithValue("@points", points);
						insertCmd.ExecuteNonQuery();

						// Update user's total points
						string updatePointsQuery = "UPDATE users SET total_points = total_points + @points WHERE id = @userId";
						MySqlCommand updateCmd = new MySqlCommand(updatePointsQuery, con, transaction);
						updateCmd.Parameters.AddWithValue("@points", points);
						updateCmd.Parameters.AddWithValue("@userId", userId);
						updateCmd.ExecuteNonQuery();

						// Commit transaction
						transaction.Commit();

						return new SubmissionResult
						{
							Success = true,
							Message = "Answer submitted successfully!",
							PointsAwarded = points
						};
					}
					catch (Exception)
					{
						transaction.Rollback();
						throw;
					}
				}
			}
			catch (Exception e)
			{
				return new SubmissionResult
				{
					Success = false,
					Message = "Error submitting answer: " + e.Message,
					PointsAwarded = 0
				};
			}
		}

		/// <summary>
		/// Get user's total points
		/// </summary>
		[WebMethod(EnableSession = true)]
		public int GetUserPoints(int userId)
		{
			try
			{
				string query = "SELECT total_points FROM users WHERE id = @userId";

				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();
					MySqlCommand cmd = new MySqlCommand(query, con);
					cmd.Parameters.AddWithValue("@userId", userId);

					object result = cmd.ExecuteScalar();
					return result != null ? Convert.ToInt32(result) : 0;
				}
			}
			catch (Exception e)
			{
				throw new Exception("Error fetching user points: " + e.Message);
			}
		}
	}

	/////////////////////////////////////////////////////////////////////////
	//DATA CLASSES FOR ESSAY QUESTIONS
	/////////////////////////////////////////////////////////////////////////

	public class EssayQuestion
	{
		public int Id { get; set; }
		public string Title { get; set; }
		public string QuestionText { get; set; }
		public int Points { get; set; }
		public DateTime CreatedAt { get; set; }
		public bool Active { get; set; }
		public bool HasSubmitted { get; set; }
		public DateTime? SubmittedAt { get; set; }
	}

	public class EssayQuestionDetail
	{
		public int Id { get; set; }
		public string Title { get; set; }
		public string QuestionText { get; set; }
		public int Points { get; set; }
		public DateTime CreatedAt { get; set; }
		public bool Active { get; set; }
		public bool HasSubmitted { get; set; }
		public string SubmittedAnswer { get; set; }
		public DateTime? SubmittedAt { get; set; }
		public int? PointsAwarded { get; set; }
	}

	public class SubmissionResult
	{
		public bool Success { get; set; }
		public string Message { get; set; }
		public int PointsAwarded { get; set; }
	}
}