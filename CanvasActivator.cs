﻿/*==============================================================================================================================
| Author        Katie Oliveras
| Client        Seattle University
| Project       Canvas Curriculum
\=============================================================================================================================*/
using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using OnTopic.AspNetCore.Mvc.Controllers;
using OnTopic.AspNetCore.Mvc.Host.Components;
using OnTopic.Data.Caching;
using OnTopic.Data.Sql;
using OnTopic.Editor.AspNetCore.Attributes;
using OnTopic.Editor.AspNetCore.Controllers;
using OnTopic.Internal.Diagnostics;
using OnTopic.Lookup;
using OnTopic.Mapping;
using OnTopic.Mapping.Hierarchical;
using OnTopic.Repositories;
using OnTopic.ViewModels;

namespace OnTopic.AspNetCore.Mvc.Host {

  /*============================================================================================================================
  | CLASS: SAMPLE ACTIVATOR
  \---------------------------------------------------------------------------------------------------------------------------*/
  /// <summary>
  ///   Responsible for creating instances of factories in response to web requests. Represents the Composition Root for
  ///   Dependency Injection.
  /// </summary>
  [ExcludeFromCodeCoverage]
  public class CanvasActivator : IControllerActivator, IViewComponentActivator {

    /*==========================================================================================================================
    | PRIVATE INSTANCES
    \-------------------------------------------------------------------------------------------------------------------------*/
    private readonly            ITypeLookupService              _typeLookupService;
    private readonly            ITopicMappingService            _topicMappingService;
    private readonly            ITopicRepository                _topicRepository;
    private readonly            IWebHostEnvironment             _webHostEnvironment;
    private readonly            StandardEditorComposer          _standardEditorComposer;

    /*==========================================================================================================================
    | HIERARCHICAL TOPIC MAPPING SERVICE
    \-------------------------------------------------------------------------------------------------------------------------*/
    private readonly IHierarchicalTopicMappingService<NavigationTopicViewModel> _hierarchicalMappingService;

    /*==========================================================================================================================
    | CONSTRUCTOR
    \-------------------------------------------------------------------------------------------------------------------------*/
    /// <summary>
    ///   Establishes a new instance of the <see cref="CanvasActivator"/>, including any shared dependencies to be used across
    ///   instances of controllers.
    /// </summary>
    /// <remarks>
    ///   The constructor is responsible for establishing dependencies with the singleton lifestyle so that they are available
    ///   to all requests.
    /// </remarks>
    public CanvasActivator(string connectionString, IWebHostEnvironment webHostEnvironment) {

      /*------------------------------------------------------------------------------------------------------------------------
      | Verify dependencies
      \-----------------------------------------------------------------------------------------------------------------------*/
      Contract.Requires(webHostEnvironment, nameof(webHostEnvironment));

      /*------------------------------------------------------------------------------------------------------------------------
      | Initialize Topic Repository
      \-----------------------------------------------------------------------------------------------------------------------*/
                                _webHostEnvironment             = webHostEnvironment;
      var                       sqlTopicRepository              = new SqlTopicRepository(connectionString);
      var                       cachedTopicRepository           = new CachedTopicRepository(sqlTopicRepository);

      /*------------------------------------------------------------------------------------------------------------------------
      | Preload repository
      \-----------------------------------------------------------------------------------------------------------------------*/
      _topicRepository                                          = cachedTopicRepository;
      _typeLookupService                                        = new DynamicTopicViewModelLookupService();
      _topicMappingService                                      = new TopicMappingService(_topicRepository, _typeLookupService);
      _                                                         = _topicRepository.Load();

      /*------------------------------------------------------------------------------------------------------------------------
      | INITIALIZE EDITOR COMPOSER
      \-----------------------------------------------------------------------------------------------------------------------*/
      _standardEditorComposer   = new(_topicRepository, _webHostEnvironment);

      /*------------------------------------------------------------------------------------------------------------------------
      | Establish hierarchical topic mapping service
      \-----------------------------------------------------------------------------------------------------------------------*/
      _hierarchicalMappingService = new HierarchicalTopicMappingService<NavigationTopicViewModel>(
        _topicRepository,
        _topicMappingService
      );

    }

    /*==========================================================================================================================
    | METHOD: CREATE
    \-------------------------------------------------------------------------------------------------------------------------*/
    /// <summary>
    ///   Registers dependencies, and injects them into new instances of controllers in response to each request.
    /// </summary>
    /// <returns>A concrete instance of an <see cref="Controller"/>.</returns>
    public object Create(ControllerContext context) {

      /*------------------------------------------------------------------------------------------------------------------------
      | Validate parameters
      \-----------------------------------------------------------------------------------------------------------------------*/
      Contract.Requires(context, nameof(context));

      /*------------------------------------------------------------------------------------------------------------------------
      | Determine controller type
      \-----------------------------------------------------------------------------------------------------------------------*/
      var type = context.ActionDescriptor.ControllerTypeInfo.AsType();

      /*------------------------------------------------------------------------------------------------------------------------
      | Configure and return appropriate controller
      \-----------------------------------------------------------------------------------------------------------------------*/
      return type.Name switch {
        nameof(TopicController) =>
          new TopicController(_topicRepository, _topicMappingService),
        nameof(SitemapController) =>
          new SitemapController(_topicRepository),
        nameof(EditorController) => new EditorController(_topicRepository, _topicMappingService),
        _ => throw new InvalidOperationException($"Unknown controller {type.Name}")
      };

    }

    /// <summary>
    ///   Registers dependencies, and injects them into new instances of view components in response to each request.
    /// </summary>
    /// <returns>A concrete instance of an <see cref="ViewComponent"/>.</returns>
    public object Create(ViewComponentContext context) {

      /*------------------------------------------------------------------------------------------------------------------------
      | Validate parameters
      \-----------------------------------------------------------------------------------------------------------------------*/
      Contract.Requires(context, nameof(context));

      /*------------------------------------------------------------------------------------------------------------------------
      | Determine view component type
      \-----------------------------------------------------------------------------------------------------------------------*/
      var type = context.ViewComponentDescriptor.TypeInfo.AsType();

      /*------------------------------------------------------------------------------------------------------------------------
      | Configure and return appropriate view component
      \-----------------------------------------------------------------------------------------------------------------------*/

      //Handle standard topic editor view components
      if (StandardEditorComposer.IsEditorComponent(type)) {
        return _standardEditorComposer.ActivateEditorComponent(
          type,
          _topicRepository
        );
      }

      //Handle project-specific view components
      return type.Name switch {
        nameof(MenuViewComponent) =>
          new MenuViewComponent(_topicRepository, _hierarchicalMappingService),
        nameof(PageLevelNavigationViewComponent) =>
          new PageLevelNavigationViewComponent(_topicRepository, _hierarchicalMappingService),
        _ => throw new InvalidOperationException($"Unknown view component {type.Name}")
      };

    }

    /*==========================================================================================================================
    | METHOD: RELEASE
    \-------------------------------------------------------------------------------------------------------------------------*/
    /// <summary>
    ///   Responds to a request to release resources associated with a particular controller.
    /// </summary>
    public void Release(ControllerContext context, object controller) { }

    /// <summary>
    ///   Responds to a request to release resources associated with a particular view component.
    /// </summary>
    public void Release(ViewComponentContext context, object viewComponent) { }

  } //Class
} //Namespace