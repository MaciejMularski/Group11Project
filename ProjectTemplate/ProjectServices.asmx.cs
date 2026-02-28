using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Web.Services;

namespace ProjectTemplate
{
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [System.Web.Script.Services.ScriptService]
    public class ProjectServices : System.Web.Services.WebService
    {
        ////////////////////////////////////////////////////////////////////////
        /// Database credentials - UPDATE THESE FOR YOUR LOCAL SETUP
        ////////////////////////////////////////////////////////////////////////
        private string dbServer = "107.180.1.16";
        private string dbPort = "3306";
        private string dbID = "cis440Spring2026team11";
        private string dbPass = "cis440Spring2026team11";
        private string dbName = "cis440Spring2026team11";
        ////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////
        /// Connection string method
        ////////////////////////////////////////////////////////////////////////
        private string getConString()
        {
            return "SERVER=" + dbServer + "; PORT=" + dbPort + "; DATABASE=" + dbName + 
                   "; UID=" + dbID + "; PASSWORD=" + dbPass;
        }
        ////////////////////////////////////////////////////////////////////////

        [WebMethod(EnableSession = true)]
        public string TestConnection()
        {
            try
            {
                string testQuery = "SELECT 1";
                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(testQuery, con))
                {
                    con.Open();
                    cmd.ExecuteScalar();
                }
                return "Success! Connected to " + dbName;
            }
            catch (Exception e)
            {
                return "Connection failed. Error: " + e.Message;
            }
        }

        /////////////////////////////////////////////////////////////////////////
        // SPRINT 2 - ESSAY QUESTIONS FEATURE
        /////////////////////////////////////////////////////////////////////////

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

        [WebMethod(EnableSession = true)]
        public SubmissionResult SubmitEssayAnswer(int userId, int questionId, string answerText)
        {
            try
            {
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

                    MySqlTransaction transaction = con.BeginTransaction();

                    try
                    {
                        string insertQuery = @"
                            INSERT INTO essay_submissions (user_id, question_id, answer_text, points_awarded)
                            VALUES (@userId, @questionId, @answerText, @points)";

                        MySqlCommand insertCmd = new MySqlCommand(insertQuery, con, transaction);
                        insertCmd.Parameters.AddWithValue("@userId", userId);
                        insertCmd.Parameters.AddWithValue("@questionId", questionId);
                        insertCmd.Parameters.AddWithValue("@answerText", answerText);
                        insertCmd.Parameters.AddWithValue("@points", points);
                        insertCmd.ExecuteNonQuery();

                        string updatePointsQuery = "UPDATE users SET total_points = total_points + @points WHERE id = @userId";
                        MySqlCommand updateCmd = new MySqlCommand(updatePointsQuery, con, transaction);
                        updateCmd.Parameters.AddWithValue("@points", points);
                        updateCmd.Parameters.AddWithValue("@userId", userId);
                        updateCmd.ExecuteNonQuery();

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

        /////////////////////////////////////////////////////////////////////////
        // REWARDS FEATURE (from teammates)
        /////////////////////////////////////////////////////////////////////////

        [WebMethod(EnableSession = true)]
        public Reward[] GetRewards()
        {
            if (Session["userId"] == null)
                throw new Exception("Not logged in");

            var rewards = new List<Reward>();

            using (MySqlConnection con = new MySqlConnection(getConString()))
            using (MySqlCommand cmd = new MySqlCommand(@"
                SELECT reward_id, name, description, points_cost, quantity_available, is_active, image_url
                FROM rewards
                WHERE is_active = 1
                ORDER BY points_cost ASC;
            ", con))
            {
                con.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        rewards.Add(new Reward
                        {
                            rewardId = r.GetInt32("reward_id"),
                            name = r.GetString("name"),
                            description = r.IsDBNull(r.GetOrdinal("description")) ? "" : r.GetString("description"),
                            pointsCost = r.GetInt32("points_cost"),
                            quantityAvailable = r.GetInt32("quantity_available"),
                            isActive = r.GetInt32("is_active") == 1,
                            imageUrl = r.IsDBNull(r.GetOrdinal("image_url")) ? "" : r.GetString("image_url")
                        });
                    }
                }
            }
            return rewards.ToArray();
        }

        [WebMethod(EnableSession = true)]
        public Redemption[] GetMyRedemptions()
        {
            if (Session["userId"] == null)
                throw new Exception("Not logged in");

            int userId = (int)Session["userId"];
            var redemptions = new List<Redemption>();

            using (MySqlConnection con = new MySqlConnection(getConString()))
            using (MySqlCommand cmd = new MySqlCommand(@"
                SELECT rr.redemption_id, rr.reward_id, rw.name AS reward_name,
                       rr.points_spent, rr.status, rr.created_at
                FROM reward_redemptions rr
                JOIN rewards rw ON rw.reward_id = rr.reward_id
                WHERE rr.employee_id = @id
                ORDER BY rr.created_at DESC;
            ", con))
            {
                cmd.Parameters.AddWithValue("@id", userId);
                con.Open();

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        redemptions.Add(new Redemption
                        {
                            redemptionId = r.GetInt32("redemption_id"),
                            rewardId = r.GetInt32("reward_id"),
                            rewardName = r.GetString("reward_name"),
                            pointsSpent = r.GetInt32("points_spent"),
                            status = r.GetString("status"),
                            createdAt = r.GetDateTime("created_at")
                        });
                    }
                }
            }

            return redemptions.ToArray();
        }

        [WebMethod(EnableSession = true)]
        public RedeemResult RedeemReward(int rewardId)
        {
            if (Session["userId"] == null)
                return new RedeemResult { success = false, message = "Not logged in" };

            int userId = (int)Session["userId"];

            using (MySqlConnection con = new MySqlConnection(getConString()))
            {
                con.Open();

                using (MySqlTransaction tx = con.BeginTransaction())
                {
                    try
                    {
                        int cost, qty, active;

                        using (MySqlCommand getReward = new MySqlCommand(@"
                            SELECT points_cost, quantity_available, is_active
                            FROM rewards
                            WHERE reward_id = @rid
                            FOR UPDATE;
                        ", con, tx))
                        {
                            getReward.Parameters.AddWithValue("@rid", rewardId);

                            using (var r = getReward.ExecuteReader())
                            {
                                if (!r.Read())
                                {
                                    tx.Rollback();
                                    return new RedeemResult { success = false, message = "Reward not found." };
                                }

                                cost = r.GetInt32("points_cost");
                                qty = r.GetInt32("quantity_available");
                                active = r.GetInt32("is_active");
                            }
                        }

                        if (active != 1)
                        {
                            tx.Rollback();
                            return new RedeemResult { success = false, message = "Reward not available." };
                        }

                        if (qty <= 0)
                        {
                            tx.Rollback();
                            return new RedeemResult { success = false, message = "Reward out of stock." };
                        }

                        int points;
                        using (MySqlCommand getPts = new MySqlCommand(@"
                            SELECT points
                            FROM employees
                            WHERE employee_id = @id
                            FOR UPDATE;
                        ", con, tx))
                        {
                            getPts.Parameters.AddWithValue("@id", userId);
                            object result = getPts.ExecuteScalar();

                            if (result == null || result == DBNull.Value)
                            {
                                tx.Rollback();
                                return new RedeemResult { success = false, message = "Employee not found." };
                            }

                            points = Convert.ToInt32(result);
                        }

                        if (points < cost)
                        {
                            tx.Rollback();
                            return new RedeemResult { success = false, message = "Insufficient points." };
                        }

                        using (MySqlCommand updPts = new MySqlCommand(@"
                            UPDATE employees
                            SET points = points - @cost
                            WHERE employee_id = @id;
                        ", con, tx))
                        {
                            updPts.Parameters.AddWithValue("@cost", cost);
                            updPts.Parameters.AddWithValue("@id", userId);
                            updPts.ExecuteNonQuery();
                        }

                        using (MySqlCommand ins = new MySqlCommand(@"
                            INSERT INTO reward_redemptions (employee_id, reward_id, points_spent, status, created_at)
                            VALUES (@id, @rid, @cost, 'Pending', NOW());
                        ", con, tx))
                        {
                            ins.Parameters.AddWithValue("@id", userId);
                            ins.Parameters.AddWithValue("@rid", rewardId);
                            ins.Parameters.AddWithValue("@cost", cost);
                            ins.ExecuteNonQuery();
                        }

                        using (MySqlCommand updInv = new MySqlCommand(@"
                            UPDATE rewards
                            SET quantity_available = quantity_available - 1
                            WHERE reward_id = @rid AND quantity_available > 0;
                        ", con, tx))
                        {
                            updInv.Parameters.AddWithValue("@rid", rewardId);
                            int rows = updInv.ExecuteNonQuery();

                            if (rows == 0)
                            {
                                tx.Rollback();
                                return new RedeemResult { success = false, message = "Reward out of stock." };
                            }
                        }

                        int newPoints;
                        using (MySqlCommand getNew = new MySqlCommand(@"
                            SELECT points
                            FROM employees
                            WHERE employee_id = @id;
                        ", con, tx))
                        {
                            getNew.Parameters.AddWithValue("@id", userId);
                            newPoints = Convert.ToInt32(getNew.ExecuteScalar());
                        }

                        tx.Commit();
                        return new RedeemResult
                        {
                            success = true,
                            message = "Redeemed successfully!",
                            newPoints = newPoints
                        };
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }

                        return new RedeemResult
                        {
                            success = false,
                            message = "Server error: " + ex.Message
                        };
                    }
                }
            }
        }

        /////////////////////////////////////////////////////////////////////////
        // LOGIN / REGISTER / SESSION FEATURES (from teammates)
        /////////////////////////////////////////////////////////////////////////

        [WebMethod(EnableSession = true)]
        public RegisterResult RegisterEmployee(string email, string password, string role)
        {
            RegisterResult resp = new RegisterResult { success = false, newEmployeeId = 0 };

            if (Session["userId"] == null)
            {
                resp.message = "Not logged in.";
                return resp;
            }

            string sessionRole = (string)Session["role"];
            if (sessionRole == null || sessionRole.ToLower() != "admin")
            {
                resp.message = "Unauthorized. Admins only.";
                return resp;
            }

            email = (email ?? "").Trim().ToLower();
            password = (password ?? "").Trim();
            role = (role ?? "employee").Trim().ToLower();

            if (email.Length == 0 || !email.Contains("@"))
            {
                resp.message = "Please enter a valid email.";
                return resp;
            }

            if (password.Length < 8)
            {
                resp.message = "Password must be at least 8 characters.";
                return resp;
            }

            if (role != "employee" && role != "admin")
            {
                resp.message = "Role must be employee or admin.";
                return resp;
            }

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                {
                    con.Open();

                    using (MySqlCommand check = new MySqlCommand(
                        "SELECT COUNT(*) FROM employees WHERE email = @email;", con))
                    {
                        check.Parameters.AddWithValue("@email", email);
                        int exists = Convert.ToInt32(check.ExecuteScalar());
                        if (exists > 0)
                        {
                            resp.message = "That email is already registered.";
                            return resp;
                        }
                    }

                    using (MySqlCommand cmd = new MySqlCommand(@"
                        INSERT INTO employees (email, password, role, points, failed_attempts, is_locked, last_login_point_date)
                        VALUES (@email, @password, @role, 0, 0, 0, NULL);
                        SELECT LAST_INSERT_ID();", con))
                    {
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@password", password);
                        cmd.Parameters.AddWithValue("@role", role);

                        resp.newEmployeeId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    resp.success = true;
                    resp.message = "Employee registered successfully.";
                    return resp;
                }
            }
            catch (Exception e)
            {
                resp.message = "Server error: " + e.Message;
                return resp;
            }
        }

        [WebMethod(EnableSession = true)]
        public LoginResult Login(string email, string password)
        {
            LoginResult resp = new LoginResult();

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                {
                    con.Open();

                    string sql = @"
                        SELECT employee_id, password, role, points, failed_attempts, is_locked, last_login_point_date
                        FROM employees
                        WHERE email = @email
                        LIMIT 1;
                    ";

                    int employeeId;
                    string dbPassword;
                    string role;
                    int points;
                    int failedAttempts;
                    bool locked;
                    DateTime? lastLoginPointDate = null;
                    
                    using (MySqlCommand cmd = new MySqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@email", email);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                resp.success = false;
                                resp.message = "Invalid email or password.";
                                return resp;
                            }

                            employeeId = reader.GetInt32("employee_id");
                            dbPassword = reader.GetString("password");
                            role = reader.GetString("role");
                            points = reader.GetInt32("points");
                            failedAttempts = reader.GetInt32("failed_attempts");
                            locked = reader.GetInt32("is_locked") == 1;

                            int dateIndex = reader.GetOrdinal("last_login_point_date");

                            if (!reader.IsDBNull(dateIndex))
                            {
                                lastLoginPointDate = reader.GetDateTime(dateIndex).Date;
                            }
                        }
                    }

                    if (locked)
                    {
                        resp.success = false;
                        resp.isLocked = true;
                        resp.remainingAttempts = 0;
                        resp.message = "Account locked after 3 failed attempts.";
                        return resp;
                    }

                    if (password == dbPassword)
                    {
                        DateTime today = DateTime.Today;

                        bool giveLoginPoint = !lastLoginPointDate.HasValue || lastLoginPointDate.Value < today;

                        if (giveLoginPoint)
                        {
                            using (MySqlCommand award = new MySqlCommand(@"
                                UPDATE employees
                                SET points = points + 1,
                                    last_login_point_date = CURDATE(),
                                    failed_attempts = 0,
                                    is_locked = 0
                                WHERE employee_id = @id;", con))
                            {
                                award.Parameters.AddWithValue("@id", employeeId);
                                award.ExecuteNonQuery();
                            }

                            points += 1;
                        }
                        else
                        {
                            using (MySqlCommand reset = new MySqlCommand(@"
                                UPDATE employees
                                SET failed_attempts = 0,
                                    is_locked = 0
                                WHERE employee_id = @id;", con))
                            {
                                reset.Parameters.AddWithValue("@id", employeeId);
                                reset.ExecuteNonQuery();
                            }
                        }

                        Session["userId"] = employeeId;
                        Session["role"] = role;

                        resp.success = true;
                        resp.message = giveLoginPoint ? "Login successful. +1 daily login point!" : "Login successful.";
                        resp.userId = employeeId;
                        resp.role = role;
                        resp.points = points;
                        resp.remainingAttempts = 3;
                        return resp;
                    }

                    failedAttempts += 1;
                    bool nowLocked = failedAttempts >= 3;

                    using (MySqlCommand upd = new MySqlCommand(@"
                        UPDATE employees
                        SET failed_attempts = @fa, is_locked = @locked
                        WHERE employee_id = @id;", con))
                    {
                        upd.Parameters.AddWithValue("@fa", failedAttempts);
                        upd.Parameters.AddWithValue("@locked", nowLocked ? 1 : 0);
                        upd.Parameters.AddWithValue("@id", employeeId);
                        upd.ExecuteNonQuery();
                    }

                    resp.success = false;
                    resp.isLocked = nowLocked;
                    resp.remainingAttempts = Math.Max(0, 3 - failedAttempts);
                    resp.message = nowLocked
                        ? "Account locked after 3 failed attempts."
                        : $"Invalid email or password. Attempts remaining: {resp.remainingAttempts}";
                    return resp;
                }
            }
            catch (Exception e)
            {
                resp.success = false;
                resp.message = "Server error: " + e.Message;
                return resp;
            }
        }

        [WebMethod(EnableSession = true)]
        public MeResult Me()
        {
            if (Session["userId"] == null)
            {
                return new MeResult
                {
                    loggedIn = false,
                    userId = 0,
                    role = ""
                };
            }

            return new MeResult
            {
                loggedIn = true,
                userId = (int)Session["userId"],
                role = (string)Session["role"]
            };
        }

        [WebMethod(EnableSession = true)]
        public int GetPoints()
        {
            if (Session["userId"] == null)
                throw new Exception("Not logged in");

            using (MySqlConnection con = new MySqlConnection(getConString()))
            {
                con.Open();

                MySqlCommand cmd = new MySqlCommand(
                    "SELECT points FROM employees WHERE employee_id = @id", con);

                cmd.Parameters.AddWithValue("@id", Session["userId"]);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /////////////////////////////////////////////////////////////////////////
        // DAILY PULSE CHECK FEATURE (from teammates)
        /////////////////////////////////////////////////////////////////////////

        [WebMethod(EnableSession = true)]
        public PulseStatusResult GetDailyPulseStatus()
        {
            if (Session["userId"] == null)
            {
                return new PulseStatusResult
                {
                    loggedIn = false,
                    shouldShow = false,
                    message = "Not logged in"
                };
            }

            int userId = (int)Session["userId"];

            using (MySqlConnection con = new MySqlConnection(getConString()))
            using (MySqlCommand cmd = new MySqlCommand(@"
                SELECT COUNT(*)
                FROM daily_pulse
                WHERE employee_id = @id AND response_date = CURDATE();
            ", con))
            {
                cmd.Parameters.AddWithValue("@id", userId);
                con.Open();

                int count = Convert.ToInt32(cmd.ExecuteScalar());

                return new PulseStatusResult
                {
                    loggedIn = true,
                    shouldShow = (count == 0),
                    message = (count == 0) ? "Pulse check pending" : "Already submitted today"
                };
            }
        }

        [WebMethod(EnableSession = true)]
        public SubmitPulseResult SubmitDailyPulse(string mood)
        {
            if (Session["userId"] == null)
                return new SubmitPulseResult { success = false, message = "Not logged in" };

            int userId = (int)Session["userId"];

            if (string.IsNullOrWhiteSpace(mood) || mood.Length > 16)
                return new SubmitPulseResult { success = false, message = "Invalid mood" };

            using (MySqlConnection con = new MySqlConnection(getConString()))
            {
                con.Open();

                using (MySqlTransaction tx = con.BeginTransaction())
                {
                    try
                    {
                        using (MySqlCommand insert = new MySqlCommand(@"
                            INSERT INTO daily_pulse (employee_id, mood, response_date)
                            VALUES (@id, @mood, CURDATE());
                        ", con, tx))
                        {
                            insert.Parameters.AddWithValue("@id", userId);
                            insert.Parameters.AddWithValue("@mood", mood);
                            insert.ExecuteNonQuery();
                        }

                        using (MySqlCommand award = new MySqlCommand(@"
                            UPDATE employees
                            SET points = points + 5
                            WHERE employee_id = @id;
                        ", con, tx))
                        {
                            award.Parameters.AddWithValue("@id", userId);
                            award.ExecuteNonQuery();
                        }

                        int points;
                        using (MySqlCommand getPts = new MySqlCommand(@"
                            SELECT points FROM employees WHERE employee_id = @id;
                        ", con, tx))
                        {
                            getPts.Parameters.AddWithValue("@id", userId);
                            points = Convert.ToInt32(getPts.ExecuteScalar());
                        }

                        tx.Commit();

                        return new SubmitPulseResult
                        {
                            success = true,
                            message = "Thanks! +5 points.",
                            newPoints = points
                        };
                    }
                    catch (MySqlException ex)
                    {
                        tx.Rollback();

                        if (ex.Message.ToLower().Contains("duplicate"))
                        {
                            return new SubmitPulseResult
                            {
                                success = false,
                                message = "You already submitted today's pulse."
                            };
                        }

                        return new SubmitPulseResult { success = false, message = "DB error: " + ex.Message };
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        return new SubmitPulseResult { success = false, message = "Server error: " + ex.Message };
                    }
                }
            }
        }

        [WebMethod(EnableSession = true)]
        public bool Logout()
        {
            Session.Clear();
            Session.Abandon();
            return true;
        }
    }

    /////////////////////////////////////////////////////////////////////////
    // DATA CLASSES
    /////////////////////////////////////////////////////////////////////////

    // Essay Questions Classes
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

    // Rewards Classes
    public class Reward
    {
        public int rewardId { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int pointsCost { get; set; }
        public int quantityAvailable { get; set; }
        public bool isActive { get; set; }
        public string imageUrl { get; set; }
    }

    public class Redemption
    {
        public int redemptionId { get; set; }
        public int rewardId { get; set; }
        public string rewardName { get; set; }
        public int pointsSpent { get; set; }
        public string status { get; set; }
        public DateTime createdAt { get; set; }
    }

    public class RedeemResult
    {
        public bool success { get; set; }
        public string message { get; set; }
        public int newPoints { get; set; }
    }

    // Login/Register Classes
    public class LoginResult
    {
        public bool success { get; set; }
        public string message { get; set; }
        public int userId { get; set; }
        public string role { get; set; }
        public int points { get; set; }
        public bool isLocked { get; set; }
        public int remainingAttempts { get; set; }
    }

    public class RegisterResult
    {
        public bool success { get; set; }
        public string message { get; set; }
        public int newEmployeeId { get; set; }
    }

    public class MeResult
    {
        public bool loggedIn { get; set; }
        public int userId { get; set; }
        public string role { get; set; }
    }

    // Daily Pulse Classes
    public class PulseStatusResult
    {
        public bool loggedIn { get; set; }
        public bool shouldShow { get; set; }
        public string message { get; set; }
    }

    public class SubmitPulseResult
    {
        public bool success { get; set; }
        public string message { get; set; }
        public int newPoints { get; set; }
    }
}