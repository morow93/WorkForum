﻿using BeginApplication.Context;
using BeginApplication.Models;
using BeginApplication.Models.Paged;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Security;
using WebMatrix.WebData;

namespace BeginApplication.Repository
{
    public class ConcreteForumRepository: IForumRepository
    {
        private SimpleMembershipContext context = new SimpleMembershipContext();

        #region Таблицы    

        public IQueryable<UserProfile> UserProfiles
        {
            get { return context.UserProfiles; }
        }

        public IQueryable<UserProperty> UserProperties
        {
            get { return context.UserProperties; }
        }

        public IQueryable<Section> Sections
        {
            get { return context.Sections; }
        }

        public IQueryable<Theme> Themes
        {
            get { return context.Themes; }
        }

        public IQueryable<Comment> Comments
        {
            get { return context.Comments; }
        }

        public IQueryable<Like> Likes
        {
            get { return context.Likes; }
        }

        #endregion

        #region Выборка для форума

        /// <summary>
        /// Получить все темы конкретного юзера
        /// </summary>
        public List<ShortThemeInfo> GetThemesByUser(int id)
        {
            return context.Themes.Where(t => t.UserId == id).OrderByDescending(t => t.CreationDate).Select(t => new ShortThemeInfo {
                ThemeId = t.ThemeId,
                ThemeTitle = t.ThemeTitle,
                CreationDate = t.CreationDate
            }).ToList();
        }

        /// <summary>
        /// Получить последние добавленные темы
        /// </summary>
        public List<ThemeInfo> GetRecentThemes(int count, int? userId)
        {
            return (from themes in context.Themes
                    join users in context.UserProfiles on themes.UserId equals users.UserId
                    into usersGroup
                    from users in usersGroup.DefaultIfEmpty()
                    join comments in context.Comments on themes.ThemeId equals comments.ThemeId
                    into commentsGroup
                    from comments in commentsGroup.DefaultIfEmpty()
                    group new { themes, users, comments }
                    by new { users.UserId, users.UserName, themes.ThemeId, themes.ThemeTitle, themes.CreationDate }
                    into groupResult
                    select new ThemeInfo
                    {
                        UserId = groupResult.Key.UserId,
                        UserName = groupResult.Key.UserName ?? "Удаленный пользователь",
                        ThemeId = groupResult.Key.ThemeId,
                        ThemeTitle = groupResult.Key.ThemeTitle,
                        CreationDate = groupResult.Key.CreationDate,
                        CountComments = groupResult.Where(x => x.comments.IsAdmitted).Select(c => c.comments.CommentId).Count(x => x != null), 
                        CountUncheckedComments = groupResult.Where(x => !x.comments.IsAdmitted).Select(c => c.comments.CommentId).Count(x => x != null)
                    }).OrderByDescending(ti => ti.CreationDate).Take(count).ToList();
        }

        /// <summary>
        /// Получить все разделы форума
        /// </summary>
        public List<SectionInfo> GetForumSections(int? userId)
        {
            var query = (from sections in context.Sections
                         join themes in context.Themes on sections.SectionId equals themes.SectionId 
                         into themesGroup from themes in themesGroup.DefaultIfEmpty()
                         join comments in context.Comments on themes.ThemeId equals comments.ThemeId 
                         into commentGroup from comments in commentGroup.DefaultIfEmpty()
                         group new { sections, themes, comments } by new { sections.SectionId, sections.SectionTitle } 
                         into groupResult
                         select new SectionInfo
                         {
                             SectionId = groupResult.Key.SectionId,
                             SectionTitle = groupResult.Key.SectionTitle,
                             ThemeCount = groupResult.Select(s => s.themes.ThemeId).Distinct().Count(x => x != null),
                             CommentCount = groupResult.Where(x => x.comments.IsAdmitted).Select(c => c.comments.CommentId).Count(x => x != null),
                             CountUncheckedComments = groupResult.Where(x => !x.comments.IsAdmitted).Select(c => c.comments.CommentId).Count(x => x != null)
                         }).OrderBy(x => x.SectionTitle).ToList();
            return query;
        }

        /// <summary>
        /// Получить все темы конкретного раздела
        /// </summary>
        public List<ThemeInfo> GetThemesBySection(int id, int? userId)
        {
            return (from themes in context.Themes
                    join users in context.UserProfiles on themes.UserId equals users.UserId
                    into usersGroup
                    from users in usersGroup.DefaultIfEmpty()
                    join comments in context.Comments on themes.ThemeId equals comments.ThemeId
                    into commentsGroup
                    from comments in commentsGroup.DefaultIfEmpty()
                    where themes.SectionId == id
                    group new { themes, users, comments }
                    by new { users.UserId, users.UserName, themes.ThemeId, themes.ThemeTitle, themes.CreationDate }
                    into groupResult
                    select new ThemeInfo
                    {
                        UserId = groupResult.Key.UserId,
                        UserName = groupResult.Key.UserName ?? "Удаленный пользователь",
                        ThemeId = groupResult.Key.ThemeId,
                        ThemeTitle = groupResult.Key.ThemeTitle,
                        CreationDate = groupResult.Key.CreationDate,
                        CountComments = groupResult.Where(x => x.comments.IsAdmitted).Select(c => c.comments.CommentId).Count(x => x != null),
                        CountUncheckedComments = groupResult.Where(x => !x.comments.IsAdmitted).Select(c => c.comments.CommentId).Count(x => x != null)
                    }).OrderByDescending(ti => ti.CreationDate).ToList();
        }

        /// <summary>
        /// Получить все комментарии конкретной темы
        /// </summary>
        /// <param name="id">id темы</param>
        /// <param name="showAll">есть ли права на просмотр всех сообщений</param>
        /// <param name="userId">id юзера, производящего выборку</param>
        /// <returns></returns>
        public List<CommentInfo> GetCommentsByTheme(int id, bool showAll, int? userId)
        {
            var query = new List<Comment>();

            if (showAll)
            {
                query = context.Comments.Where(c => c.ThemeId == id).ToList();
            }
            else
            {
                if (userId == null)
                    query = context.Comments.Where(c => c.ThemeId == id && c.IsAdmitted).ToList();
                else
                    query = context.Comments.Where(c => c.ThemeId == id && (c.UserId == userId || c.IsAdmitted)).ToList();
            }

            return query.Select(x => new CommentInfo
                        {
                            CommentId = x.CommentId,
                            CommentText = x.CommentText,
                            CreationDate = x.CreationDate,
                            UserName = x.User.UserName ?? "Удаленный пользователь",
                            UserId = x.User.UserId,
                            CommentVote = x.Like.Where(y => y.CommentId == x.CommentId).Sum(z => z.Vote),
                            isAvatarExists = x.User.ImageData == null ? false : true,
                            isAdmitted = x.IsAdmitted
                        }).OrderBy(c => c.CreationDate).ToList();
        }

        /// <summary>
        /// Получить все сообщения, еще не прошедшие модерацию
        /// </summary>
        /// <returns></returns>
        public List<CommentAdmittedInfo> GetNotAdmittedComments()
        {
            return context.Comments.Where(c => !c.IsAdmitted).Select(c => new CommentAdmittedInfo
            {
                CommentId = c.CommentId,
                CommentText = c.CommentText,
                CreationDate = c.CreationDate,
                ThemeId = c.ThemeId,
                ThemeTitle = c.Theme.ThemeTitle,
                UserId = c.UserId,
                UserName = c.User.UserName ?? "Удаленный пользователь"
            }).OrderByDescending(c => c.CreationDate).ToList();
        }

        #endregion

        #region Вставка

        public void AddTheme(Theme theme)
        {
            try
            {
                context.Themes.Add(theme);
                context.SaveChanges();
            }
            catch { }
        }

        public void AddComment(Comment comment)
        {
            try
            {
                context.Comments.Add(comment);
                context.SaveChanges();
            }
            catch { }
        }

        public bool AddSection(Section section)
        {
            var result = true;
            try
            {
                context.Sections.Add(section);
                context.SaveChanges();
            }
            catch {
                result = false;
            }
            return result;
        }

        #endregion

        #region Настройки приватности

        public PrivatePropertiesModel GetPrivateProperties(int id)
        {
            var model = new PrivatePropertiesModel();

            var isExist = context
                .UserProperties
                .FirstOrDefault(x => x.UserId == id);

            if (isExist == null)
            {
                model.ShowEmail = model.ShowMobile = false;
            }
            else
            {
                model.ShowEmail = isExist.ShowEmail;
                model.ShowMobile = isExist.ShowMobile;
            }

            var mobile = context.UserProfiles.FirstOrDefault(x => x.UserId == id).Mobile;
            model.Mobile = mobile == null ? String.Empty : mobile;

            return model;
        }

        public bool SetPrivateProperties(int id)
        {
            if (context.UserProfiles.FirstOrDefault(u => u.UserId == id) != null)
            {
                var userProperty = context.UserProperties.FirstOrDefault(u => u.UserId == id);
                if (userProperty == null)
                {
                    userProperty = new UserProperty();
                    userProperty.UserId = id;
                    userProperty.ShowEmail = false;
                    userProperty.ShowMobile = false;
                    context.UserProperties.Add(userProperty);
                    context.SaveChanges();
                    return true;
                }
                return false;
            }
            return false;
            
        }

        public void SetPrivateProperties(PrivatePropertiesModel model, int id)
        {
            var existProperty = context.UserProperties.FirstOrDefault(x => x.UserId == id);

            if (existProperty == null)
            {
                context.UserProperties.Add(new UserProperty
                {
                    UserId = id,
                    ShowEmail = (bool)model.ShowEmail,
                    ShowMobile = (bool)model.ShowMobile
                });
            }
            else
            {
                existProperty.ShowEmail = model.ShowEmail;
                existProperty.ShowMobile = model.ShowMobile;
            }

            var existProfile = context.UserProfiles.FirstOrDefault(x => x.UserId == id);
            existProfile.Mobile = model.Mobile;

            context.SaveChanges();
        }

        public PrivateCabinetModel GetPrivateCabinet(int userId)
        {
            var user = context.UserProfiles.FirstOrDefault(u => u.UserId == userId);

            if (user.UserProperty == null) SetPrivateProperties(user.UserId);
            var result = new PrivateCabinetModel
            {
                isAvatarExist = user.ImageData == null ? false : true,
                RegistrationDate = user.RegistrationDate,
                Email = user.UserProperty.ShowEmail ? (string.IsNullOrEmpty(user.Email) ? "пользователь не указал почту" : user.Email) : "пользователь скрыл почту",
                Mobile = user.UserProperty.ShowMobile ? (string.IsNullOrEmpty(user.Mobile) ? "пользователь не указал телефон" : user.Mobile) : "пользователь скрыл телефон"
            };         
            var query = context.Likes.Where(lk=>lk.Comment.UserId == userId);
            if (query == null || query.Count() == 0)            
                result.UserRating = 0;            
            else            
                result.UserRating = query.Sum(x => x.Vote);
            
            return result;
        }

        #endregion

        #region Удаление

        public bool RemoveTheme(int id)
        {
            var theme = context.Themes.FirstOrDefault(u => u.ThemeId == id);
            var result = true;

            if (theme != null)
            {                
                try 
                {
                    foreach (var comment in theme.Comment.ToList())
                    {
                        foreach (var like in comment.Like.ToList())
                        {
                            context.Likes.Remove(like);
                        }
                        context.Comments.Remove(comment);
                    }
                    context.Themes.Remove(theme);

                    context.SaveChanges();
                }
                catch
                {
                    result = false;
                }
            }
            return result;
        }

        public bool RemoveComment(int id)
        {
            var comment = context.Comments.FirstOrDefault(c => c.CommentId == id);
            var result = true;

            if (comment != null)
            {
                try
                {
                    foreach (var like in comment.Like.ToList())
                    {
                        context.Likes.Remove(like);
                    }
                    context.Comments.Remove(comment);

                    context.SaveChanges();
                }
                catch
                {
                    result = false;
                }
            }
            return result;
        }

        public bool RemoveUser(UserModel user)
        {
            var result = true;

            try
            {               
                var roles = Roles.GetRolesForUser(user.UserName);
                if (roles != null && roles.Length != 0)
                {
                    Roles.RemoveUserFromRoles(user.UserName, roles);
                }

                using (var context = new SimpleMembershipContext())
                {
                    var toRemove = context.UserProfiles.FirstOrDefault(u => u.UserId == user.UserId);

                    toRemove.UserName = null;
                    toRemove.Email = null;
                    toRemove.ImageData = null;
                    toRemove.ImageMimeType = null;
                    toRemove.Mobile = null;

                    context.SaveChanges();
                }
            }
            catch
            {
                result = false;
            }
            return result;
        }

        public bool RemoveSection(int id)
        {
            var section = context.Sections.FirstOrDefault(x => x.SectionId == id);
            var result = true;

            if (section != null)
            {
                try
                {
                    foreach (var theme in section.Theme.ToList())
                    {
                        foreach (var comment in theme.Comment.ToList())
                        {
                            foreach (var like in comment.Like.ToList())
                            {
                                context.Likes.Remove(like);
                            }
                            context.Comments.Remove(comment);
                        }
                        context.Themes.Remove(theme);
                    }
                    context.Sections.Remove(section);

                    context.SaveChanges();
                }
                catch
                {
                    result = false;
                }
            }
            return result;
        }

        #endregion

        /// <summary>
        /// Получить информацию о юзере
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public UserSummaryModel GetUserSummary(int id)
        {
            var result = context.UserProfiles.Where(u=>u.UserId == id).Select(x => new UserSummaryModel
            {
                UserId = x.UserId,
                UserName = x.UserName,
                Rating = context.Likes.Where(lk => lk.Comment.UserId == id).Sum(y => y.Vote),
                Email = x.UserProperty.ShowEmail ? (string.IsNullOrEmpty(x.Email) ? "пользователь не указал почту" : x.Email) : "пользователь скрыл почту",
                Mobile = x.UserProperty.ShowMobile ? (string.IsNullOrEmpty(x.Mobile) ? "пользователь не указал телефон" : x.Mobile) : "пользователь скрыл телефон",
                isAvatarExists = x.ImageData == null ? false : true,
                RegistrationDate = x.RegistrationDate
            }).FirstOrDefault();
            return result;
        }

        /// <summary>
        /// Допустить комментарий
        /// </summary>
        public bool AdmittComment(int id)
        {
            try
            {
                var comment = context.Comments.FirstOrDefault(c => c.CommentId == id);

                if (!comment.IsAdmitted)
                    comment.IsAdmitted = true;
                else
                    throw new Exception("Не должно вылетать");

                context.SaveChanges();
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}